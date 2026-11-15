# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$payload=Join-Path $projectRoot 'build/payload'
if(-not(Test-Path -LiteralPath (Join-Path $payload 'agent-pal.exe'))){throw 'Build first'}
$target=Join-Path $payload 'DragGaitTests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=(Get-ChildItem (Join-Path $projectRoot 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:DragGaitTests /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'DragGaitTests.cs')
if($LASTEXITCODE -ne 0){throw 'Drag gait tests compile failed'}
$savedMode=$env:AGENT_PAL_TEST_MODE;$savedState=$env:AGENT_PAL_STATE_DIR
try{
    $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-drag-gait-'+[Guid]::NewGuid().ToString('N'))
    & $target
    if($LASTEXITCODE -ne 0){throw 'Drag gait tests failed'}
    Write-Output ('PASS isolated drag gait state: '+$env:AGENT_PAL_STATE_DIR)
}finally{$env:AGENT_PAL_TEST_MODE=$savedMode;$env:AGENT_PAL_STATE_DIR=$savedState;if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}}
