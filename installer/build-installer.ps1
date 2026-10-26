param([string]$Version = '', [string]$Dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe", [string]$Compiler = '')
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
if(!$Version) { $project = [xml](Get-Content (Join-Path $sourceRoot 'ResetExplode.csproj') -Raw); $Version = @($project.Project.PropertyGroup.Version | Where-Object {$_})[0] }
if($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versao numerica invalida' }
$packageRoot = Split-Path $sourceRoot -Parent
if (!$Compiler) { $Compiler = Join-Path $packageRoot 'ResetExplode-build-tools\InnoPortable\tools\ISCC.exe' }
foreach($file in @($Compiler,$Dotnet)) { if(!(Test-Path -LiteralPath $file)) { throw "Ferramenta ausente: $file" } }
$signature = Get-AuthenticodeSignature -LiteralPath $Compiler
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B.V.') { throw 'Compilador sem assinatura oficial valida' }
$expected = @{
    'PawnIO_setup.exe' = '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'
    'VC_redist.x64.exe' = 'CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B'
}
foreach($name in $expected.Keys) {
    $file = Join-Path $PSScriptRoot "dependencies\$name"
    if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $expected[$name]) { throw "Dependencia com hash diferente: $name" }
    if((Get-AuthenticodeSignature -LiteralPath $file).Status -ne 'Valid') { throw "Dependencia com assinatura invalida: $name" }
}
Push-Location $sourceRoot
try {
    & $Dotnet run --project tests/StressPolicyTests.csproj -c Release
    if($LASTEXITCODE -ne 0) { throw 'Testes termicos falharam' }
    & $Dotnet run --project tests/UpdaterTests.csproj -c Release
    if($LASTEXITCODE -ne 0) { throw 'Testes do atualizador falharam' }
    & $Dotnet publish ResetExplode.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None "-p:Version=$Version" -o (Join-Path $packageRoot 'ResetExplode-publish') --nologo
    if($LASTEXITCODE -ne 0) { throw 'Compilacao falhou' }
    & $Compiler /Qp "/DAppVersion=$Version" (Join-Path $PSScriptRoot 'ResetExplode-Setup.iss')
    if($LASTEXITCODE -ne 0) { throw 'Empacotamento falhou' }
    & (Join-Path $PSScriptRoot 'sign-update.ps1') -Version $Version
}
finally { Pop-Location }
