[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework x64 C# compiler not found.' }
$outputDirectory = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& $compilerPath /nologo /target:winexe /platform:x64 "/out:$outputDirectory\CodexSkillViewer.exe" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\SkillViewer.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CodexSkillViewer.exe.config') -Destination $outputDirectory -Force
Write-Output "Built: $outputDirectory\CodexSkillViewer.exe"
