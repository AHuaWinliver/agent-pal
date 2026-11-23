param(
 [Parameter(Mandatory=$true)][ValidateSet('sun-wukong','bailong')][string]$Id,
 [Parameter(Mandatory=$true)][string]$OutputDirectory
)
# SPDX-License-Identifier: Apache-2.0
# Rebuild reviewed artwork locally without a generator service or API key.
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Choose a new output directory; existing packs are never overwritten.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$build=Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$target=Join-Path $build 'BuildCompanionRevision.exe'
$sources=@('tools/BuildOriginalCharacters.cs','tools/BuildOriginalCharacters.Custom.cs','src/DesktopTasks/AgentPal/PetCharacter.cs','src/DesktopTasks/AgentPal/CompanionMotion.cs','src/DesktopTasks/AgentPal/DefaultCompanionCatalog.cs','src/DesktopTasks/AgentPal/CharacterPackCatalog.cs') | ForEach-Object {Join-Path $root $_}
& $compiler /nologo /target:exe /optimize+ /main:BuildCompanionRevision /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $sources
if($LASTEXITCODE -ne 0){throw 'Companion renderer compile failed.'}
$art=Join-Path $root ('art/companion-rigs/'+$Id)
& $target $art (Join-Path $art 'profile.json') $output
if($LASTEXITCODE -ne 0){throw 'Companion export failed.'}
Write-Output ('Rebuilt pack: '+$output)
Write-Output ('Actual-render arm audit: '+$output+'.arm-audit.json')
