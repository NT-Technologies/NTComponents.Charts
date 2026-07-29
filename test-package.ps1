param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,
    [string[]] $Frameworks = @('net9.0', 'net10.0', 'net11.0')
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path $PackagePath).Path
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "ntcomponents-charts-package-$([Guid]::NewGuid().ToString('N'))"
$feed = Join-Path $temporaryRoot 'feed'

try {
    New-Item $feed -ItemType Directory -Force | Out-Null
    Copy-Item $package $feed

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $entries = @($archive.Entries.FullName)
        $nuspecEntry = $archive.Entries | Where-Object FullName -Like '*.nuspec' | Select-Object -First 1
        if (-not $nuspecEntry) {
            throw 'The package does not contain a nuspec.'
        }

        $reader = [IO.StreamReader]::new($nuspecEntry.Open())
        try {
            [xml] $nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $metadata = $nuspec.package.metadata
        if ($metadata.id -ne 'NTComponents.Charts') {
            throw "Expected package ID NTComponents.Charts, found '$($metadata.id)'."
        }

        $version = [string] $metadata.version
        foreach ($framework in $Frameworks) {
            if (-not ($entries -contains "lib/$framework/NTComponents.Charts.dll")) {
                throw "The package is missing lib/$framework/NTComponents.Charts.dll."
            }
        }
        if (-not ($entries -contains 'staticwebassets/ntcomponents-charts.js')) {
            throw 'The package is missing its directly importable chart JavaScript module.'
        }
        if ($entries -contains 'staticwebassets/NTComponents.Charts.lib.module.js') {
            throw 'The package contains the obsolete fingerprinted initializer module name.'
        }

        $forbiddenDependencies = @('AspNetCore.SassCompiler', 'SkiaSharp.NativeAssets.Linux', 'SkiaSharp.NativeAssets.WebAssembly')
        $dependencies = @($metadata.dependencies.group.dependency | ForEach-Object id)
        foreach ($dependency in $forbiddenDependencies) {
            if ($dependencies -contains $dependency) {
                throw "Build-only or redundant dependency '$dependency' leaked into the package."
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    foreach ($framework in $Frameworks) {
        $consumerRoot = Join-Path $temporaryRoot $framework
        New-Item $consumerRoot -ItemType Directory -Force | Out-Null
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$framework</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RestoreSources>$feed;https://api.nuget.org/v3/index.json</RestoreSources>
    <RestorePackagesPath>$temporaryRoot/packages</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NTComponents.Charts" Version="$version" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $consumerRoot 'Consumer.csproj')
        @"
Console.WriteLine(typeof(NTComponents.Charts.NTLineSeries<SmokePoint>).FullName);
internal sealed record SmokePoint(double X, decimal Y);
"@ | Set-Content (Join-Path $consumerRoot 'Program.cs')

        dotnet restore (Join-Path $consumerRoot 'Consumer.csproj') --force-evaluate
        if ($LASTEXITCODE -ne 0) {
            throw "Package restore failed for $framework."
        }
        dotnet build (Join-Path $consumerRoot 'Consumer.csproj') --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Package consumer build failed for $framework."
        }
    }

    Write-Host "Package $version passed structure and consumer validation for $($Frameworks -join ', ')."
}
finally {
    Remove-Item $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}
