param([Parameter(Mandatory=$true)][string]$PayloadDirectory)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$target=Join-Path $PayloadDirectory 'DefaultCompanionTests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=(Get-ChildItem (Join-Path $project 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:DefaultCompanionTests /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll $sources (Join-Path $PSScriptRoot 'DefaultCompanionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Default companion tests failed to compile.'}
$saved=@{}
foreach($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_TEST_ASSETS')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
 $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path ([IO.Path]::GetTempPath()) ('ap-default-test-'+[guid]::NewGuid().ToString('N'))
 & $target
 if($LASTEXITCODE -ne 0){throw 'Default companion integration failed.'}
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')};Remove-Item -LiteralPath $target -Force}
