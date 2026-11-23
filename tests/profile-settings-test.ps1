# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $projectRoot 'build/ProfileSettingsTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks/AgentPal') -Filter '*.cs' -File | ForEach-Object FullName)
& $compiler /nologo /target:exe /main:ProfileSettingsTests /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll $sources (Join-Path $PSScriptRoot 'ProfileSettingsTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Profile settings tests failed to compile.' }
$saved = @{}
foreach ($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_TEST_ASSETS')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:AGENT_PAL_TEST_MODE = '1'
    $env:AGENT_PAL_STATE_DIR = Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-profile-test-' + [Guid]::NewGuid().ToString('N'))
    & $target (Join-Path $projectRoot 'build/payload/assets')
    if ($LASTEXITCODE -ne 0) { throw 'Profile settings tests failed.' }
} finally {
    foreach ($name in $saved.Keys) {
        if ($null -eq $saved[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    }
}
