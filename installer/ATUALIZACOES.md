# Canal de atualizacoes v2

O codigo completo recuperado fica na branch main de brandtgustavo2001-afk/ResetExplode-Updates. O antigo fluxo que publicava um executavel sem manifesto foi substituido por validacao e publicacao assinada.

## Migracao unica

A chave privada da 0.16.8 nao foi localizada. Nao e possivel gerar uma assinatura valida para os clientes antigos usando uma chave nova. O instalador 0.16.9 configura a confianca v2 sem apagar AppData. Nao desinstale antes. Nenhuma verificacao criptografica foi removida.

Clientes antigos continuam consultando update.json, que conserva o manifesto assinado da 0.16.8. Clientes migrados consultam update-v2.json. A proxima versao assinada e baixada ao abrir (apos 20 segundos), a cada 4 horas ou pelo botao Verificar agora; e aplicada quando o aplicativo encerra. Nao e necessario baixar instaladores manualmente depois da migracao. O aplicativo e elevado, portanto o Windows ainda pode pedir autorizacao conforme a politica UAC do computador.

## Publicar a proxima versao

1. Envie e valide as alteracoes na branch main.
2. Em Actions, execute Publicar atualizacao assinada e informe uma versao numerica maior que a ultima publicada.
3. O fluxo executa os testes termicos e do atualizador, verifica as ferramentas e dependencias, compila o aplicativo com a versao informada e gera o instalador.
4. O manifesto e assinado com RESETEXPLODE_SIGNING_KEY_B64, guardada no cofre de segredos do repositorio. A chave e comparada a UpdateTrust.cs antes de assinar.
5. A release e criada como rascunho. Seus arquivos sao baixados e sua assinatura, hash e tamanho sao verificados. So entao a release e publicada como mais recente. Uma release publicada nao e sobrescrita.

O build bloqueia mudancas nos hashes das dependencias: quando Microsoft ou PawnIO lancarem um novo pacote, confira a origem e assinatura antes de atualizar o hash no script.

## Recuperacao

A copia local da chave fica em %USERPROFILE%/.config/ResetExplode/publishing/update-signing-v2.dpapi, protegida por DPAPI para esta conta do Windows. O segredo no GitHub permite continuar publicando mesmo que a pasta local do projeto seja apagada. Segredos do GitHub nao podem ser baixados; nao apague ou sobrescreva esse segredo nem gere outra chave sem um plano de migracao.

O codigo e a chave publica podem ser publicados. Chaves privadas, arquivos DPAPI e variaveis de assinatura nao devem aparecer em arquivos de release, logs ou commits.

## Verificacao

Execute dotnet run --project tests/UpdaterTests.csproj -c Release e dotnet run --project tests/StressPolicyTests.csproj -c Release. Os testes cobrem assinatura, adulteracao, origem do pacote, downgrade, cache, rede, integridade, limites e cancelamento. Eles nao substituem o teste final da instalacao e aplicacao no Windows.
