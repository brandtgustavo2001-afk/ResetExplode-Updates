# Contexto do ResetExplode

- Repositório canônico: `brandtgustavo2001-afk/ResetExplode-Updates` (remote local `updates`). O remote `origin` aponta ao código original e não é o destino de publicação.
- Graphify: workspace `revisa-mais`, repository_id `05dd0a0d-f9ad-43c8-8718-41db06887309`.
- Preferência do usuário: economizar tokens usando Graphify para localizar símbolos, dependências e testes antes de carregar arquivos inteiros.
- Comece com consultas específicas e respostas resumidas: `query_graph` com `skeleton=true`, `budget=1500` e poucos resultados. Expanda somente as definições necessárias.
- Confira a revisão indexada. O grafo não substitui a leitura do diff local, de arquivos novos ou de mudanças posteriores ao índice. Se indisponível ou incompleto, use `rg` e leia trechos pequenos.
- Não repita inventários, documentação ou arquivos já lidos sem necessidade. Use limites curtos nas saídas e resuma a verificação.
- Testes: `dotnet run --project tests/UpdaterTests.csproj -c Release`, `tests/StressPolicyTests.csproj` e `tests/MonitoringTests.csproj`. Execute os apropriados à mudança; não rode testes de carga de hardware para verificar alterações comuns.
- Preserve as chaves privadas fora do repositório e mantenha o fluxo de atualização assinada. Documentação de publicação: `installer/ATUALIZACOES.md`.
