param([Parameter(Mandatory=$true)][string]$PayloadDirectory)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$stage=Join-Path ([IO.Path]::GetTempPath()) ('ap-shell-ui-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$test=Join-Path $stage 'QuickTaskShellTests.exe'
$sources=(Get-ChildItem (Join-Path $project 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /main:QuickTaskShellTests /out:$test /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll $sources (Join-Path $PSScriptRoot 'QuickTaskShellTests.cs')
if($LASTEXITCODE -ne 0){throw 'Shell UI test compilation failed.'}
$saved=@{}
foreach($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_TEST_ASSETS')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
 $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path $stage 'state';$env:AGENT_PAL_TEST_ASSETS=Join-Path $PayloadDirectory 'assets'
 & $test (Join-Path $PayloadDirectory 'agent-pal.exe') $env:AGENT_PAL_TEST_ASSETS
 if($LASTEXITCODE -ne 0){throw 'Shell taskbar integration test failed.'}
 Write-Output ('Shell UI evidence: '+$env:AGENT_PAL_STATE_DIR)
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
