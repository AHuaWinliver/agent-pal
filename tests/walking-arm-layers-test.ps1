# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$target=Join-Path $root 'build/WalkingArmLayersTests.exe'
& $compiler /nologo /target:exe /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'WalkingArmLayersTests.cs')
if($LASTEXITCODE -ne 0){throw 'Walking arm layer test compile failed.'}
$renderer=Join-Path $root 'build/WalkingArmRendererTests.exe'
$sources=@('tools/BuildOriginalCharacters.cs','tools/BuildOriginalCharacters.Custom.cs','src/DesktopTasks/AgentPal/PetCharacter.cs','src/DesktopTasks/AgentPal/CompanionMotion.cs','src/DesktopTasks/AgentPal/DefaultCompanionCatalog.cs','src/DesktopTasks/AgentPal/CharacterPackCatalog.cs')|ForEach-Object{Join-Path $root $_}
& $compiler /nologo /target:exe /optimize+ /main:BuildOriginalCharacters /out:$renderer /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $sources
if($LASTEXITCODE -ne 0){throw 'Reviewed walking renderer compile failed.'}
& $target $renderer (Join-Path $root 'art')
if($LASTEXITCODE -ne 0){throw 'Walking arm layering test failed.'}
