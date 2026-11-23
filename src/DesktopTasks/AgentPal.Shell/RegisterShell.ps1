param(
 [ValidateSet('Register','Remove','Check')][string]$Action='Register',
 [Parameter(Mandatory=$true)][string]$ApplicationRoot,
 [Parameter(Mandatory=$true)][string]$StateDirectory,
 [Parameter(Mandatory=$true)][string]$Report,
 [Parameter(Mandatory=$true)][string]$ExpectedUser,
 [switch]$TrustPreviewCertificate
)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$result=@{Success=$false;Error=''}
try {
 if([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne $ExpectedUser){throw 'Use the same Windows account for menu registration; elevation under another account is not supported.'}
 $application=[IO.Path]::GetFullPath($ApplicationRoot).TrimEnd('\')
 $state=[IO.Path]::GetFullPath($StateDirectory)
 $metadata=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'deployment.json') -Raw | ConvertFrom-Json
 if($metadata.name -ne 'AgentPal.ContextMenu'){throw 'Unexpected identity package.'}
 $registration=Join-Path $state 'shell-registration.json'
 $previous=if(Test-Path -LiteralPath $registration){Get-Content -LiteralPath $registration -Raw | ConvertFrom-Json}else{$null}
 $existing=Get-AppxPackage -Name $metadata.name | Where-Object {$_.Publisher -eq $metadata.publisher} | Select-Object -First 1
 if($Action -eq 'Remove') {
  # An old copy must not remove the menu registered by a newer installation.
  if($previous -and $previous.ApplicationRoot -eq $application){if($existing){Remove-AppxPackage -Package $existing.PackageFullName};Remove-Item -LiteralPath $registration -Force}
  $result.Success=$true
 } elseif($Action -eq 'Check') {
  $result.Success=[bool]($existing -and $previous -and $previous.ApplicationRoot -eq $application)
 } else {
  if(-not(Test-Path -LiteralPath (Join-Path $application 'agent-pal.exe'))){throw 'The agent-pal application is missing.'}
  if($metadata.unsigned){throw 'This identity package is unsigned. Install a signed agent-pal build.'}
  $certificate=Join-Path $PSScriptRoot 'agent-pal-preview.cer'
  if($TrustPreviewCertificate){
   if(-not(Test-Path -LiteralPath $certificate)){throw 'The dedicated preview certificate is missing.'}
   $cert=New-Object Security.Cryptography.X509Certificates.X509Certificate2($certificate)
   if($cert.Subject -ne $metadata.publisher -or $cert.NotAfter -lt (Get-Date)){throw 'The preview certificate does not match this package or has expired.'}
   if($cert.Extensions.EnhancedKeyUsages.Value -notcontains '1.3.6.1.5.5.7.3.3'){throw 'Expected a dedicated code-signing certificate.'}
   # Explicit user consent is obtained by the app before the UAC launch.
   # Never write Root, change Developer Mode, or import a private key.
   $store=New-Object Security.Cryptography.X509Certificates.X509Store('TrustedPeople','LocalMachine')
   try{$store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite);$store.Add($cert)}finally{$store.Close()}
  }
  $same=$existing -and $previous -and $previous.ApplicationRoot -eq $application -and [string]$existing.Version -eq $metadata.version
  if(-not $same){
   if($existing){Remove-AppxPackage -Package $existing.PackageFullName}
   try {Add-AppxPackage -Path (Join-Path $PSScriptRoot 'agent-pal.ContextMenu.msix') -ExternalLocation $application}
   catch {
    $original=$_
    if($previous -and (Test-Path -LiteralPath (Join-Path $previous.ApplicationRoot 'shell/agent-pal.ContextMenu.msix'))){
     try {Add-AppxPackage -Path (Join-Path $previous.ApplicationRoot 'shell/agent-pal.ContextMenu.msix') -ExternalLocation $previous.ApplicationRoot}catch{}
    }
    throw $original
   }
  }
  New-Item -ItemType Directory -Path $state -Force | Out-Null
  @{ApplicationRoot=$application;Publisher=$metadata.publisher;Version=$metadata.version}|ConvertTo-Json|Set-Content -LiteralPath $registration -Encoding UTF8
  $result.Success=$true
 }
} catch { $result.Error=$_.Exception.Message }
$result|ConvertTo-Json|Set-Content -LiteralPath $Report -Encoding UTF8
if(-not $result.Success){exit 1}
