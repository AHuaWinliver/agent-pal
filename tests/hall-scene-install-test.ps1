param([string]$InstallerPath)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$version=[IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
$installer=Join-Path $projectRoot ('dist/agent-pal-'+$version+'-Setup.exe')
if($InstallerPath){$installer=[IO.Path]::GetFullPath($InstallerPath)}
if(-not(Test-Path -LiteralPath $installer)){throw 'Build first'}
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-hall-install-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$saved=@{}
foreach($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_INSTALL_ROOT','AGENT_PAL_CLAUDE_CONFIG_DIR','AGENT_PAL_STATE_DIR')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
    $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_INSTALL_ROOT=Join-Path $fixture 'app';$env:AGENT_PAL_CLAUDE_CONFIG_DIR=Join-Path $fixture 'claude';$env:AGENT_PAL_STATE_DIR=Join-Path $fixture 'state'
    function Install-Fixture {
        $diagnostics=Join-Path $fixture 'installer-stderr.txt'
        $process=Start-Process -FilePath $installer -ArgumentList '--silent' -WorkingDirectory $fixture -WindowStyle Hidden -RedirectStandardError $diagnostics -Wait -PassThru
        if($process.ExitCode -ne 0){
            $detail=Get-Content -LiteralPath $diagnostics -Raw -Encoding utf8
            throw ('Isolated installation failed ('+$process.ExitCode+'): '+$detail)
        }
        $shortcut=Join-Path $env:AGENT_PAL_INSTALL_ROOT 'test-start-menu/AgentPal/AgentPal.lnk'
        if(-not(Test-Path -LiteralPath $shortcut -PathType Leaf)){throw 'AgentPal Start menu shortcut missing after installation'}
    }
    Install-Fixture
    $scene=Join-Path $env:AGENT_PAL_INSTALL_ROOT 'scenes/sect-hall-v2.png'
    if(-not(Test-Path -LiteralPath $scene)){throw 'Scene folder missing after real installation'}
    $expected=(Get-FileHash -LiteralPath (Join-Path $projectRoot 'art/scenes/sect-hall-v2.png')).Hash
    if((Get-FileHash -LiteralPath $scene).Hash -ne $expected){throw 'Installed scene differs from approved art'}
    Write-Output 'PASS fresh installation actually deploys scenes folder and exact hall art'
    $helper=Join-Path $env:AGENT_PAL_INSTALL_ROOT 'HallSceneCheck.exe'
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    & $compiler /nologo /target:exe /out:$helper /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'HallSceneTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Installed hall probe failed to compile'}
    function Check-Hall([string]$kind,[string]$name){
        $preview=Join-Path $fixture ($name+'.png')
        $check=Start-Process -FilePath $helper -ArgumentList @($kind,('"'+$preview+'"')) -WorkingDirectory $fixture -WindowStyle Hidden -Wait -PassThru
        if($check.ExitCode -ne 0){throw ('Installed hall render failed: '+$name)}
        Write-Output ('PASS installed production renderer: '+$name)
    }
    Check-Hall 'file' 'fresh-install'
    # Simulate the previous release's missing scene without deleting any data.
    Move-Item -LiteralPath $scene -Destination ($scene+'.fixture-original')
    Check-Hall 'embedded' 'missing-file-fallback'
    Install-Fixture
    if((Get-FileHash -LiteralPath $scene).Hash -ne $expected){throw 'Upgrade did not repair missing scene'}
    Check-Hall 'file' 'reinstall-repaired'
    [IO.File]::WriteAllText($scene,'fixture: intentionally invalid PNG')
    Check-Hall 'embedded' 'corrupt-file-fallback'
    Install-Fixture
    if((Get-FileHash -LiteralPath $scene).Hash -ne $expected){throw 'Upgrade did not repair corrupted scene'}
    Check-Hall 'file' 'corruption-repaired'
    Write-Output ('PASS isolated install/upgrade and background fallbacks; evidence: '+$fixture)
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
