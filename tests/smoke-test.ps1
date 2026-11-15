# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$app=Join-Path $projectRoot 'build/payload/agent-pal.exe'
if(-not(Test-Path -LiteralPath $app)){throw 'Run build.ps1 first'}
$savedTestMode=$env:AGENT_PAL_TEST_MODE
$savedState=$env:AGENT_PAL_STATE_DIR
try {
    $env:AGENT_PAL_TEST_MODE='1'
    $env:AGENT_PAL_STATE_DIR=Join-Path $projectRoot ('build/smoke/'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $env:AGENT_PAL_STATE_DIR -Force | Out-Null
    foreach($language in @('en','zh-CN')) {
        [IO.File]::WriteAllText((Join-Path $env:AGENT_PAL_STATE_DIR 'ui-language.txt'),$language,[Text.UTF8Encoding]::new($false))
        foreach($mode in @('--self-test','--encoding-self-test','--claude-task-self-test','--reminder-self-test')) {
            $check=Start-Process -FilePath $app -ArgumentList $mode -WindowStyle Hidden -PassThru -Wait
            if($check.ExitCode -ne 0){throw ($language+' '+$mode+' failed: '+$check.ExitCode)}
            Write-Output ('PASS '+$language+' '+$mode)
        }
    }
}finally{$env:AGENT_PAL_TEST_MODE=$savedTestMode;$env:AGENT_PAL_STATE_DIR=$savedState}
