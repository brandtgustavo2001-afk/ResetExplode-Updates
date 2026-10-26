# RESET EXPLODE — Prévia de stress de CPU controlado

Esta cópia foi construída a partir do código público do RESET EXPLODE v0.16.0 (licença MIT). Ela **não** é uma atualização direta da versão instalada v0.16.8 e **não** inclui necessariamente todos os recursos mais novos, como as atualizações automáticas. É uma prévia independente para avaliar o novo teste, sem substituir o programa em C:\Program Files\ResetExplode.

## Mudanças do teste de CPU

- Seleção de alvo de 25%, 50%, 75% ou 100%; inicia em 25% por padrão.
- O alvo descreve uma fração aproximada de tempo ativo de cada trabalhador, e não um limite fixo de energia ou temperatura. O uso real depende do escalonador, do Turbo Boost e do hardware.
- Os trabalhadores ficam defasados no tempo para distribuir a atividade por núcleo, em vez de executar longas sequências simultâneas sem descanso.
- Começa a 10% e aumenta no máximo 5 pontos por amostra de sensor (~1 segundo), até o valor selecionado.
- Perto do limite térmico reduz progressivamente a carga; ao alcançar o limite, encerra.
- Se a CPU não fornecer temperatura válida ou as leituras ficarem sem atualização por mais de 5 segundos, a carga é interrompida.
- O limite configurável foi restringido a 95 °C (90 °C padrão). Não desabilita proteções de hardware/BIOS.
- O registro mostra a intensidade selecionada, e o painel apresenta o alvo atual após controle térmico.

## Verificação

Execute `dotnet run --project tests/StressPolicyTests.csproj` para validar a política de segurança (requer o SDK .NET 8).

O aplicativo pode exigir execução como administrador para acessar os sensores. Não execute testes de CPU se o resfriamento estiver com defeito. Compare a temperatura CPU Package do aplicativo com o HWiNFO durante a mesma carga.
