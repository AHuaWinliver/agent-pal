param([string]$BuildDirectory)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
if(-not $BuildDirectory){$BuildDirectory=Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-workshop-presentation-'+[guid]::NewGuid().ToString('N'))}
New-Item -ItemType Directory -Path $BuildDirectory -Force | Out-Null
$target=Join-Path $BuildDirectory 'WorkshopPresentationTests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=(Get-ChildItem (Join-Path $project 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:WorkshopPresentationTests /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll "/resource:$project/art/scenes/sect-hall-v2.png,AgentPal.SectHall.Background.png" $sources (Join-Path $PSScriptRoot 'WorkshopPresentationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Workshop presentation tests failed to compile.'}
$saved=@{}
foreach($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_TEST_ASSETS')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
 $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path $BuildDirectory 'state';$env:AGENT_PAL_TEST_ASSETS=Join-Path $project 'build/payload/assets'
 & $target
 if($LASTEXITCODE -ne 0){throw 'Workshop presentation tests failed.'}
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
