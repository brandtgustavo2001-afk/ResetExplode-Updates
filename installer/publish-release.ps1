param([Parameter(Mandatory)][string]$Version, [string]$Gh = 'gh')
$ErrorActionPreference = 'Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versao invalida' }
$repo = 'brandtgustavo2001-afk/ResetExplode-Updates'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$releaseDir = Join-Path (Split-Path $sourceRoot -Parent) 'ResetExplode-instalador'
$fileName = "ResetExplode-Setup-$Version-x64.exe"
$stage = Join-Path $sourceRoot "dist\release-$Version"
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item (Join-Path $releaseDir $fileName) (Join-Path $stage $fileName)
Copy-Item (Join-Path $releaseDir "update-v2-$Version.json") (Join-Path $stage 'update-v2.json')
Copy-Item (Join-Path $PSScriptRoot 'legacy-update.json') (Join-Path $stage 'update.json')
function Verify-Release([string]$Folder) {
    $envelope = Get-Content (Join-Path $Folder 'update-v2.json') -Raw | ConvertFrom-Json
    $payload = [Convert]::FromBase64String($envelope.Payload)
    $source = Get-Content (Join-Path $sourceRoot 'UpdateTrust.cs') -Raw
    $key = [regex]::Match($source,'PublicKey = "([^"]+)"').Groups[1].Value
    $rsa = [Security.Cryptography.RSA]::Create()
    try {
        $rsa.ImportSubjectPublicKeyInfo([Convert]::FromBase64String($key),[ref]$null)
        if(!$rsa.VerifyData($payload,[Convert]::FromBase64String($envelope.Signature),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)) { throw 'Assinatura do manifesto invalida' }
    } finally { $rsa.Dispose() }
    $package = [Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
    $file = Join-Path $Folder $fileName
    if($package.Version -ne $Version -or $package.Url -ne "https://github.com/$repo/releases/download/v$Version/$fileName" -or (Get-Item $file).Length -ne $package.Size -or (Get-FileHash $file -Algorithm SHA256).Hash -ne $package.Sha256) { throw 'Pacote e manifesto nao correspondem' }
}
Verify-Release $stage
$releases = & $Gh release list --repo $repo --limit 100 --json tagName,isPrerelease,isDraft
if($LASTEXITCODE -ne 0) { throw 'Falha ao consultar releases' }
$stable = @($releases | ConvertFrom-Json) | Where-Object {!$_.isPrerelease -and !$_.isDraft}
foreach($release in $stable) {
    $existing = $null
    if([Version]::TryParse($release.tagName.TrimStart('v'),[ref]$existing) -and $existing -ge [Version]$Version) { throw 'Versao igual ou inferior ja publicada; nao sobrescrever' }
}
$sha = (git -C $sourceRoot rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0) { throw 'Commit do codigo ausente' }
& $Gh release create "v$Version" (Join-Path $stage $fileName) (Join-Path $stage 'update-v2.json') (Join-Path $stage 'update.json') --repo $repo --target $sha --draft --title "RESET EXPLODE $Version" --notes-file (Join-Path $sourceRoot 'RELEASE-NOTES.md')
if($LASTEXITCODE -ne 0) { throw 'Falha ao criar rascunho' }
$verify = Join-Path $stage ('download-'+[Guid]::NewGuid().ToString('N'))
& $Gh release download "v$Version" --repo $repo --dir $verify
if($LASTEXITCODE -ne 0) { throw 'Falha ao verificar download do rascunho; release nao liberada' }
Verify-Release $verify
& $Gh release edit "v$Version" --repo $repo --draft=false --latest
if($LASTEXITCODE -ne 0) { throw 'Falha ao liberar release estavel' }
Write-Output "Release $Version publicada apos verificacao da assinatura e do download."
