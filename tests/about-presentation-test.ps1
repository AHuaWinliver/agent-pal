param([string]$OutputDirectory=(Join-Path $PSScriptRoot '../build/about-presentation'))
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$target=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'AboutPresentationTests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=(Get-ChildItem (Join-Path $root 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
$resources=@("/resource:$root/art/branding/brand-intro-mark.png,AgentPal.Brand.IntroMark.png","/resource:$root/art/branding/agentpal-spirit.png,AgentPal.Brand.Spirit.png","/resource:$root/art/branding/agentpal-wordmark.png,AgentPal.Brand.Wordmark.png")
foreach($id in @('orbit','spark','moss','pippin','sun-wukong','bailong')){$resources+="/resource:$root/art/branding/avatars/$id.png,AgentPal.Avatar.$id.png"}
& $compiler /nologo /target:exe /main:AboutPresentationTests /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll $resources $sources (Join-Path $PSScriptRoot 'AboutPresentationTests.cs')
if($LASTEXITCODE -ne 0){throw 'About tests failed to compile.'}
$saved=@{}
foreach($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
 $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'state'
 & $target
 if($LASTEXITCODE -ne 0){throw 'About presentation tests failed.'}
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
