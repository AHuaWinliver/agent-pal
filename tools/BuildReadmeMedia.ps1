# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$tool = Join-Path $projectRoot 'build/BuildReadmeMedia.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks/AgentPal') -Filter '*.cs' -File | ForEach-Object FullName)
& $compiler /nologo /target:exe /optimize+ /main:BuildReadmeMedia "/win32icon:$projectRoot/build/agent-pal.ico" /out:$tool /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll "/resource:$projectRoot/art/branding/agentpal-wordmark.png,AgentPal.Brand.Wordmark.png" "/resource:$projectRoot/art/branding/brand-intro-mark.png,AgentPal.Brand.IntroMark.png" "/resource:$projectRoot/art/branding/agentpal-spirit.png,AgentPal.Brand.Spirit.png" "/resource:$projectRoot/art/branding/avatars/orbit.png,AgentPal.Avatar.orbit.png" "/resource:$projectRoot/art/branding/avatars/spark.png,AgentPal.Avatar.spark.png" "/resource:$projectRoot/art/branding/avatars/moss.png,AgentPal.Avatar.moss.png" "/resource:$projectRoot/art/branding/avatars/sun-wukong.png,AgentPal.Avatar.sun-wukong.png" "/resource:$projectRoot/art/branding/avatars/bailong.png,AgentPal.Avatar.bailong.png" "/resource:$projectRoot/art/branding/avatars/pippin.png,AgentPal.Avatar.pippin.png" $sources (Join-Path $PSScriptRoot 'BuildReminderPreviews.cs') (Join-Path $PSScriptRoot 'BuildReadmeMedia.cs') (Join-Path $PSScriptRoot 'BuildProductMedia.cs')
if ($LASTEXITCODE -ne 0) { throw 'README media tool compilation failed' }
$previousEnvironment = @{}
foreach ($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_CLAUDE_CONFIG_DIR','AGENT_PAL_INSTALL_ROOT')) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $demoRoot = Join-Path $projectRoot ('build/readme-demo/' + [Guid]::NewGuid().ToString('N'))
    $env:AGENT_PAL_TEST_MODE = '1'
    $env:AGENT_PAL_STATE_DIR = Join-Path $demoRoot 'state'
    $env:AGENT_PAL_CLAUDE_CONFIG_DIR = Join-Path $demoRoot 'claude'
    $env:AGENT_PAL_INSTALL_ROOT = Join-Path $demoRoot 'app'
    New-Item -ItemType Directory -Force -Path $env:AGENT_PAL_STATE_DIR | Out-Null
    & $tool (Join-Path $projectRoot 'build/payload/assets') (Join-Path $projectRoot 'docs/images') (Join-Path $projectRoot 'build/previews')
    if ($LASTEXITCODE -ne 0) { throw 'README media rendering failed' }
} finally {
    foreach ($name in $previousEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$previousEnvironment[$name],'Process') }
}
