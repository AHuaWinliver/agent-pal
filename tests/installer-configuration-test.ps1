# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
$installer = Join-Path $projectRoot ('dist/agent-pal-' + $version + '-Setup.exe')
$target = Join-Path $projectRoot 'build/InstallerConfigurationTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'InstallerConfigurationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer configuration test compilation failed.' }
$saved = @{}
foreach ($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_INSTALL_ROOT','AGENT_PAL_CLAUDE_CONFIG_DIR')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $fixture = Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-installer-ui-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixture | Out-Null
    $env:AGENT_PAL_TEST_MODE = '1'; $env:AGENT_PAL_STATE_DIR = $fixture; $env:AGENT_PAL_INSTALL_ROOT = Join-Path $fixture 'app'; $env:AGENT_PAL_CLAUDE_CONFIG_DIR = Join-Path $fixture 'claude'
    & $target $installer
    if ($LASTEXITCODE -ne 0) { throw 'Installer configuration checks failed.' }
    $actual = Join-Path $fixture '已选择 chosen folder'
    $process = Start-Process -FilePath $installer -ArgumentList @('--silent', '--install-dir', ('"' + $actual + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $actual 'agent-pal.exe'))) { throw 'The silent installer did not use --install-dir.' }
    if (Test-Path -LiteralPath $env:AGENT_PAL_INSTALL_ROOT) { throw 'Installer unexpectedly wrote into the previous destination.' }
    Write-Output ('PASS real custom-directory installation and preview evidence: ' + $fixture)
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
