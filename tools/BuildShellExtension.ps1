param([string]$OutputDirectory,[string]$Compiler,[string]$MakeAppx,[string]$CertificateThumbprint,[switch]$Unsigned)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $project 'build/payload/shell'}
if(-not $Compiler){$Compiler=$env:AGENT_PAL_SHELL_COMPILER}
if(-not $MakeAppx){$MakeAppx=$env:AGENT_PAL_MAKEAPPX}
if(-not $CertificateThumbprint){$CertificateThumbprint=$env:AGENT_PAL_SHELL_SIGNER}
if(-not $Compiler){$found=Get-Command x86_64-w64-mingw32-clang++.exe -ErrorAction SilentlyContinue;if($found){$Compiler=$found.Source}}
if(-not $MakeAppx){$found=Get-Command makeappx.exe -ErrorAction SilentlyContinue;if($found){$MakeAppx=$found.Source}}
if(-not $Compiler -or -not $MakeAppx){throw 'Set AGENT_PAL_SHELL_COMPILER to the LLVM-MinGW x64 compiler and AGENT_PAL_MAKEAPPX to Windows SDK MakeAppx.exe.'}
if(-not $CertificateThumbprint -and -not $Unsigned){throw 'Provide a code-signing CertificateThumbprint, or use -Unsigned for compile-only validation.'}
$stage=Join-Path ([IO.Path]::GetTempPath()) ('agent-pal-shell-build-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$dll=Join-Path $stage 'AgentPalShell64.dll'
& $Compiler -std=c++17 -O2 -shared -static '-Wl,--no-insert-timestamp' -o $dll (Join-Path $project 'src/DesktopTasks/AgentPal.Shell/AgentPalShell.cpp') (Join-Path $project 'src/DesktopTasks/AgentPal.Shell/AgentPalShell.def') -lole32 -lshell32 -lshlwapi -ladvapi32 -luuid
if($LASTEXITCODE -ne 0){throw 'Native Explorer command compilation failed.'}
$publisher='CN=agent-pal, OID.2.25.311729368913984317654407730594956997722=1'
$signTool=Join-Path (Split-Path -Parent $MakeAppx) 'signtool.exe'
$cert=$null
if($CertificateThumbprint){
 $cert=Get-Item -LiteralPath ('Cert:/CurrentUser/My/'+$CertificateThumbprint)
 $publisher=$cert.Subject
 & $signTool sign /fd SHA256 /sha1 $CertificateThumbprint /s My $dll
 if($LASTEXITCODE -ne 0){throw 'Native DLL signing failed.'}
}
$python=Get-Command python.exe -ErrorAction SilentlyContinue
if(-not $python){throw 'Python 3 with Pillow is required for the native identity package artwork.'}
& $python.Source (Join-Path $PSScriptRoot 'BuildShellPackage.py') --dll $dll --icon (Join-Path $project 'build/agent-pal.ico') --makeappx $MakeAppx --output $OutputDirectory --publisher $publisher
if($LASTEXITCODE -ne 0){throw 'Native identity package failed validation.'}
Copy-Item -LiteralPath (Join-Path $project 'src/DesktopTasks/AgentPal.Shell/RegisterShell.ps1') -Destination (Join-Path $OutputDirectory 'RegisterShell.ps1') -Force
$toolRoot=Split-Path -Parent (Split-Path -Parent $Compiler)
$notices=Join-Path $OutputDirectory 'THIRD-PARTY-LICENSES'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
if(Test-Path -LiteralPath (Join-Path $toolRoot 'LICENSE.TXT')){Copy-Item -LiteralPath (Join-Path $toolRoot 'LICENSE.TXT') -Destination (Join-Path $notices 'LLVM-Apache-with-exceptions.txt') -Force}
$runtimeNotices=Join-Path $toolRoot 'x86_64-w64-mingw32/share/mingw32'
if(Test-Path -LiteralPath $runtimeNotices){Get-ChildItem -LiteralPath $runtimeNotices -Filter 'COPYING*' -File | Copy-Item -Destination $notices -Force}
if($cert){
 & $signTool sign /fd SHA256 /sha1 $CertificateThumbprint /s My (Join-Path $OutputDirectory 'agent-pal.ContextMenu.msix')
 if($LASTEXITCODE -ne 0){throw 'Identity package signing failed.'}
 # Only a dedicated preview publisher gets a public test certificate in the trial.
 if($cert.FriendlyName -eq 'agent-pal native context menu preview'){
  [IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'agent-pal-preview.cer'),$cert.RawData)
 }
}
Write-Output ('Built native shell files: '+$OutputDirectory)
