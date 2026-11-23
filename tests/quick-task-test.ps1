param([string]$AssetsDirectory)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$stage=Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-quick-verification-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$stub=Join-Path $stage 'FakeClaude.exe'
$test=Join-Path $stage 'QuickTaskTests.exe'
& $compiler /nologo /target:exe /out:$stub /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'QuickTaskStub.cs')
if($LASTEXITCODE -ne 0){throw 'Fake CLI compilation failed.'}
$sources=(Get-ChildItem (Join-Path $projectRoot 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:QuickTaskTests /out:$test "/win32icon:$projectRoot/build/agent-pal.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll $sources (Join-Path $PSScriptRoot 'QuickTaskTests.cs')
if($LASTEXITCODE -ne 0){throw 'Quick-task tests compilation failed.'}
$saved=@{}
foreach($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_TEST_QUICK_CAPTURE','AGENT_PAL_TEST_ASSETS')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
    $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path $stage 'state'
    if(-not $AssetsDirectory){$AssetsDirectory=Join-Path $projectRoot 'build/payload/assets'}
    $env:AGENT_PAL_TEST_ASSETS=[IO.Path]::GetFullPath($AssetsDirectory)
    & $test $stub $AssetsDirectory
    if($LASTEXITCODE -ne 0){throw 'Quick-task tests failed.'}
    Write-Output ('Evidence: '+$env:AGENT_PAL_STATE_DIR)
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
