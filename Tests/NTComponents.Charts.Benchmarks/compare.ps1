param([Parameter(Mandatory)][string]$Before, [Parameter(Mandatory)][string]$After)
function Load($dir) {
    $map = @{}
    Get-ChildItem $dir -Filter *-report-full.json | ForEach-Object {
        foreach ($b in (Get-Content $_.FullName -Raw | ConvertFrom-Json).Benchmarks) {
            $map[$b.FullName] = [pscustomobject]@{ Mean = $b.Statistics.Mean; Alloc = $b.Memory.BytesAllocatedPerOperation }
        }
    }
    $map
}
$a = Load $Before; $b = Load $After
$a.Keys | Sort-Object | ForEach-Object {
    if ($b.ContainsKey($_)) {
        [pscustomobject]@{
            Benchmark = ($_ -replace '^NTComponents\.Charts\.Benchmarks\.', '')
            BeforeUs = [math]::Round($a[$_].Mean / 1000, 1)
            AfterUs = [math]::Round($b[$_].Mean / 1000, 1)
            TimeRatio = [math]::Round($b[$_].Mean / $a[$_].Mean, 2)
            BeforeKB = [math]::Round($a[$_].Alloc / 1024, 1)
            AfterKB = [math]::Round($b[$_].Alloc / 1024, 1)
            AllocRatio = if ($a[$_].Alloc) { [math]::Round($b[$_].Alloc / $a[$_].Alloc, 2) } else { $null }
        }
    }
} | Format-Table -AutoSize
