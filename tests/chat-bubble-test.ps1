# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'build') | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testExe=Join-Path $projectRoot 'build/ChatBubbleTests.exe'
$sources=(Get-ChildItem (Join-Path $projectRoot 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:ChatBubbleTests /out:$testExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll $sources (Join-Path $PSScriptRoot 'ChatBubbleTests.cs')
if($LASTEXITCODE -ne 0){throw 'Chat UI tests compile failed'}
$savedMode=$env:AGENT_PAL_TEST_MODE;$savedDirectory=$env:AGENT_PAL_STATE_DIR
try{
    $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-chat-'+[Guid]::NewGuid().ToString('N'))
    & $testExe
    if($LASTEXITCODE -ne 0){throw 'Chat UI tests failed'}
    Write-Output ('UI preview and isolated evidence: '+$env:AGENT_PAL_STATE_DIR)
}finally{$env:AGENT_PAL_TEST_MODE=$savedMode;$env:AGENT_PAL_STATE_DIR=$savedDirectory}
