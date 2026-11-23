param([Parameter(Mandatory=$true)][string]$Dll,[string]$Compiler,[switch]$Registered)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$stage=Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-native-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$frameworkCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$stub=Join-Path $stage 'NativeQuickTaskStub.exe';$test=Join-Path $stage 'NativeShellTests.exe'
$fixture=$null
if(-not $Compiler){$Compiler=$env:AGENT_PAL_SHELL_COMPILER}
if($Compiler){
 $fixture=Join-Path $stage 'NativeShellContextFixture.dll'
 & $Compiler -std=c++17 -O2 -shared -static -o $fixture (Join-Path $PSScriptRoot 'NativeShellContextFixture.cpp') -lole32 -lshell32 -luuid
 if($LASTEXITCODE -ne 0){throw 'Context fixture compilation failed.'}
}
& $frameworkCompiler /nologo /target:winexe /out:$stub /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'NativeQuickTaskStub.cs')
if($LASTEXITCODE -ne 0){throw 'Stub compilation failed.'}
$sources=(Get-ChildItem (Join-Path $project 'src/DesktopTasks/AgentPal') -Filter '*.cs').FullName
& $frameworkCompiler /nologo /platform:x64 /target:exe /main:NativeShellTests /out:$test /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll $sources (Join-Path $PSScriptRoot 'NativeShellTests.cs')
if($LASTEXITCODE -ne 0){throw 'Native tests compilation failed.'}
$saved=@{}
foreach($name in @('AGENT_PAL_TEST_MODE','AGENT_PAL_STATE_DIR','AGENT_PAL_TEST_REGISTERED_SHELL')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
 $env:AGENT_PAL_TEST_MODE='1';$env:AGENT_PAL_STATE_DIR=Join-Path $stage 'state'
 $env:AGENT_PAL_TEST_REGISTERED_SHELL=if($Registered){'1'}else{$null}
 $arguments=@($Dll,$stub,(Join-Path $project 'build/agent-pal.ico'));if($fixture){$arguments+=$fixture}
 & $test $arguments
 if($LASTEXITCODE -ne 0){throw 'Native bridge validation failed.'}
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
