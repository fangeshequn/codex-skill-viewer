$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
& (Join-Path $repoPath 'build.ps1')
$exePath = Join-Path $repoPath 'dist\CodexSkillViewer.exe'
$testRoot = Join-Path $repoPath ('.test-work\' + [Guid]::NewGuid().ToString('N'))
$testProject = Join-Path $testRoot 'project'
$testCodexHome = Join-Path $testRoot 'codex-home'
$skillDirectory = Join-Path $testProject '.agents\skills\dynamic-demo'
New-Item -ItemType Directory -Path $skillDirectory,$testCodexHome,(Join-Path $testProject '.git') -Force | Out-Null
$skillFile = Join-Path $skillDirectory 'SKILL.md'
$oldCodexHome = $env:CODEX_HOME
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Get-TestInventory([string] $label) {
    $outputFile = Join-Path $testRoot ($label + '.json')
    $arguments = '--inventory "' + $outputFile + '" "' + $testProject + '"'
    $process = Start-Process -FilePath $exePath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Inventory process failed: $label" }
    return Get-Content -LiteralPath $outputFile -Raw -Encoding UTF8 | ConvertFrom-Json
}
try {
    $env:CODEX_HOME = $testCodexHome
    $process = Start-Process -FilePath $exePath -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw 'Metadata parser check failed.' }
    [IO.File]::WriteAllText($skillFile, "---`nname: dynamic-demo`ndescription: >`n  first line`n  second line`n---`n# Demo`n", $utf8)
    $first = Get-TestInventory 'added'
    $found = @($first.skills | Where-Object Name -EQ 'dynamic-demo')
    if ($found.Count -ne 1 -or $found[0].Description -ne 'first line second line') { throw 'New skill or folded description not detected.' }
    [IO.File]::WriteAllText($skillFile, "---`nname: renamed-demo`ndescription: updated description`n---`n# Updated`n", $utf8)
    $second = Get-TestInventory 'modified'
    if (@($second.skills | Where-Object Name -EQ 'dynamic-demo').Count -ne 0 -or @($second.skills | Where-Object Name -EQ 'renamed-demo').Count -ne 1) { throw 'Modified skill not refreshed.' }
    [IO.File]::WriteAllText((Join-Path $testCodexHome 'config.toml'), "[[skills.config]]`npath = '" + $skillFile + "'`nenabled = false`n", $utf8)
    $third = Get-TestInventory 'disabled'
    if (($third.skills | Where-Object Name -EQ 'renamed-demo').Enabled) { throw 'Disable configuration ignored.' }
    New-Item -ItemType Junction -Path (Join-Path $skillDirectory 'loop') -Target (Join-Path $testProject '.agents\skills') | Out-Null
    $fourth = Get-TestInventory 'junction'
    if (@($fourth.skills | Where-Object Name -EQ 'renamed-demo').Count -ne 1) { throw 'Directory cycle was not deduplicated.' }
    Remove-Item -LiteralPath $skillFile
    $last = Get-TestInventory 'removed'
    if (@($last.skills | Where-Object Name -EQ 'renamed-demo').Count -ne 0) { throw 'Deleted skill remains in fresh scan.' }
    Write-Output 'PASS: parsing, discovery, refresh, disabled config, junction cycle, deletion.'
} finally {
    $env:CODEX_HOME = $oldCodexHome
}
