param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versao numerica invalida' }
$sourceRoot = Split-Path $PSScriptRoot -Parent
$releaseDir = Join-Path (Split-Path $sourceRoot -Parent) 'ResetExplode-instalador'
$keyPath = Join-Path $env:USERPROFILE '.config\ResetExplode\publishing\update-signing-v2.dpapi'
$file = Join-Path $releaseDir "ResetExplode-Setup-$Version-x64.exe"
$payload = [ordered]@{
    Version = $Version
    Url = "https://github.com/brandtgustavo2001-afk/ResetExplode-Updates/releases/download/v$Version/ResetExplode-Setup-$Version-x64.exe"
    Sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    Size = (Get-Item -LiteralPath $file).Length
}
$bytes = [Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Compress))
$rsa = [Security.Cryptography.RSA]::Create()
$private = $null
try {
    if($env:RESETEXPLODE_SIGNING_KEY_B64) { $private = [Convert]::FromBase64String($env:RESETEXPLODE_SIGNING_KEY_B64) }
    else { $private = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($keyPath),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser) }
    $rsa.ImportPkcs8PrivateKey($private,[ref]$null)
    $public = [Convert]::ToBase64String($rsa.ExportSubjectPublicKeyInfo())
    if(!(Get-Content (Join-Path $sourceRoot 'UpdateTrust.cs') -Raw).Contains($public)) { throw 'Chave de assinatura nao corresponde ao aplicativo' }
    $signature = $rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)
    if(!$rsa.VerifyData($bytes,$signature,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)) { throw 'Falha ao verificar assinatura gerada' }
    $envelope = @{Payload=[Convert]::ToBase64String($bytes);Signature=[Convert]::ToBase64String($signature)} | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $releaseDir "update-v2-$Version.json"),$envelope)
    Write-Output "Manifesto v2 $Version assinado e verificado."
}
finally { if($private){[Array]::Clear($private,0,$private.Length)};$rsa.Dispose() }
