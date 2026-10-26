$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$toolsRoot = Join-Path (Split-Path $sourceRoot -Parent) 'ResetExplode-build-tools'
New-Item -ItemType Directory -Path $toolsRoot,(Join-Path $PSScriptRoot 'dependencies') -Force | Out-Null
Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/tools.innosetup/6.7.0/tools.innosetup.6.7.0.nupkg' -OutFile (Join-Path $toolsRoot 'InnoSetup.zip')
Expand-Archive (Join-Path $toolsRoot 'InnoSetup.zip') -DestinationPath (Join-Path $toolsRoot 'InnoPortable') -Force
$compiler = Join-Path $toolsRoot 'InnoPortable\tools\ISCC.exe'
$sig = Get-AuthenticodeSignature $compiler
if($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'Pyrsys B.V.') { throw 'Assinatura oficial do compilador invalida' }
Invoke-WebRequest 'https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe' -OutFile (Join-Path $PSScriptRoot 'dependencies\PawnIO_setup.exe')
Invoke-WebRequest 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile (Join-Path $PSScriptRoot 'dependencies\VC_redist.x64.exe')
# build-installer.ps1 validates dependency signatures and pinned hashes before use.
