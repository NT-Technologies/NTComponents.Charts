param(
    [Alias('Framework')]
    [string[]] $Frameworks = @('net10.0', 'net11.0'),
    [switch] $SkipBrowserSmoke
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Tests/AotCompatibility.TestApp/AotCompatibility.TestApp.csproj'
$artifactRoot = Join-Path $PSScriptRoot 'artifacts/aot'

function Get-BrowserPath {
    $commands = @('chrome', 'chrome.exe', 'chromium', 'chromium-browser', 'google-chrome', 'msedge', 'msedge.exe')
    foreach ($command in $commands) {
        $found = Get-Command $command -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { return $found.Source }
    }

    $candidates = @(
        "$env:ProgramFiles/Google/Chrome/Application/chrome.exe",
        "${env:ProgramFiles(x86)}/Google/Chrome/Application/chrome.exe",
        "$env:ProgramFiles/Microsoft/Edge/Application/msedge.exe",
        "${env:ProgramFiles(x86)}/Microsoft/Edge/Application/msedge.exe",
        '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
        '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge'
    )
    $installed = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if ($installed) { return $installed }

    $playwrightRoots = @(
        "$env:HOME/.cache/ms-playwright",
        "$env:LOCALAPPDATA/ms-playwright",
        "$env:USERPROFILE/Library/Caches/ms-playwright"
    )
    foreach ($root in $playwrightRoots) {
        if (-not $root -or -not (Test-Path $root)) { continue }
        $cached = Get-ChildItem $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object Name -In @('chrome', 'chrome.exe', 'Chromium') | Select-Object -First 1
        if ($cached) { return $cached.FullName }
    }

    return $null
}

foreach ($framework in $Frameworks) {
    $nativeAot = $framework -eq 'net10.0'
    $output = Join-Path $artifactRoot $framework
    $buildArtifacts = Join-Path $artifactRoot "build/$framework"
    $log = Join-Path $artifactRoot "$framework-publish.log"
    New-Item $artifactRoot -ItemType Directory -Force | Out-Null

    dotnet restore $project --force-evaluate -p:UseLocalNTComponentsProject=false -p:ArtifactsPath=$buildArtifacts
    if ($LASTEXITCODE -ne 0) { throw "Restore failed for $framework." }

    $publishOutput = dotnet publish $project --configuration Release --framework $framework --no-restore -m:1 -p:UseLocalNTComponentsProject=false -p:ArtifactsPath=$buildArtifacts -p:WasmBuildNative=$($nativeAot.ToString().ToLowerInvariant()) --output $output 2>&1
    $publishOutput | Tee-Object -FilePath $log | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "WebAssembly publish failed for $framework." }

    $analysisWarnings = @($publishOutput | Select-String -Pattern '\bIL(?:2|3)\d{3}\b')
    if ($analysisWarnings.Count -gt 0) {
        throw "AOT or trimming analysis produced $($analysisWarnings.Count) warning(s) for $framework."
    }

    $wwwroot = Join-Path $output 'wwwroot'
    if (-not (Test-Path (Join-Path $wwwroot 'index.html')) -or -not (Get-ChildItem $wwwroot -Recurse -Filter '*.wasm')) {
        throw "The $framework publish output is missing its WebAssembly application assets."
    }

    $chartAssets = Join-Path $wwwroot '_content/NTComponents.Charts'
    if (-not (Test-Path (Join-Path $chartAssets 'ntcomponents-charts.js'))) {
        throw "The $framework publish output is missing the generated chart JavaScript module."
    }
    if (Get-ChildItem $chartAssets -Recurse -File -Filter '*.ts') {
        throw "The $framework publish output contains TypeScript source or declarations."
    }

    if (-not $SkipBrowserSmoke -and $nativeAot) {
        $browser = Get-BrowserPath
        if (-not $browser) { throw 'Chrome, Chromium, or Edge is required for the browser smoke test.' }
        $python = Get-Command python3, python -ErrorAction SilentlyContinue | Sort-Object { $_.Source -match '[\\/]WindowsApps[\\/]' } | Select-Object -First 1
        if (-not $python) { throw 'Python is required to host the AOT smoke app.' }

        $port = Get-Random -Minimum 18000 -Maximum 28000
        $browserProfile = Join-Path ([IO.Path]::GetTempPath()) "ntcomponents-charts-browser-$([Guid]::NewGuid().ToString('N'))"
        $serverArguments = @{
            FilePath = $python.Source
            ArgumentList = @('-m', 'http.server', $port, '--bind', '127.0.0.1', '--directory', $wwwroot)
            PassThru = $true
        }
        if ($IsWindows) { $serverArguments.WindowStyle = 'Hidden' }
        $server = Start-Process @serverArguments
        try {
            Start-Sleep -Seconds 2
            $dom = & $browser --headless --disable-extensions --disable-gpu --no-sandbox --user-data-dir=$browserProfile --virtual-time-budget=30000 --dump-dom "http://127.0.0.1:$port/" 2>&1 | Out-String
            if ($dom -notmatch 'data-aot-smoke-ready="true"') {
                throw "The $framework browser smoke app did not reach its ready marker."
            }
            if ($dom -match 'Unhandled exception rendering component|crit: Microsoft\.AspNetCore\.Components\.WebAssembly|TypeInitialization_Type|DllNotFound|EntryPointNotFound') {
                throw "The $framework browser smoke app reported an unhandled Blazor rendering exception."
            }
        }
        finally {
            Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
            Remove-Item $browserProfile -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if (-not $nativeAot) {
        Write-Host "$framework browser rendering is unsupported by the current SkiaSharp native assets; only compilation and trimming were validated."
    }
    $mode = if ($nativeAot) { 'native WebAssembly' } else { 'trimmed WebAssembly build' }
    Write-Host "$framework passed $mode compatibility validation."
}
