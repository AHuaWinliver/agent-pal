# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$stage=Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-team-shortcuts-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=(Get-ChildItem (Join-Path $project 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
$target=Join-Path $stage 'QuickTeamTaskTests.exe'
& $compiler /nologo /target:exe /main:QuickTeamTaskTests "/win32icon:$project/build/agent-pal.ico" /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll $sources (Join-Path $PSScriptRoot 'QuickTeamTaskTests.cs')
if($LASTEXITCODE -ne 0){throw 'Team shortcut test compilation failed'}
$saved=@{}
foreach($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_TEST_ASSETS')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
 $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path $stage 'state';$env:AGENT_PAL_TEST_ASSETS=Join-Path $project 'build/payload/assets'
 & $target
 if($LASTEXITCODE -ne 0){throw 'Team shortcut tests failed'}
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
