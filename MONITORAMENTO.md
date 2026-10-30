# Monitoramento e overlay - integracao MSI / RTSS

Implementacao independente baseada nos SDKs publicos presentes nas instalacoes locais do MSI Afterburner e do RivaTuner Statistics Server. Nao inclui binarios, drivers, skins, marcas ou codigo de controle de overclock desses aplicativos.

## Fontes consultadas

- MSI Afterburner/SDK/Include/MAHMSharedMemory.h: cabecalho MAHM v2, timestamp Unix de coleta, tamanho/contagem das entradas, identificadores de origem, indice/identidade da GPU e sentinela FLT_MAX.
- MSI Afterburner/SDK/Samples/SharedMemory/MAHMSharedMemorySample: exemplo de consumo do compartilhamento MAHMSharedMemory.
- RivaTuner Statistics Server/SDK/Include/RTSSSharedMemory.h e Samples/SharedMemory/RTSSSharedMemorySample: tabela de aplicativos, campos de FPS/frametime, slots de OSD, texto estendido, contador de quadros e sincronizacao dwBusy.

## Funcionamento

Em Overlay, o modo Automatico usa o RTSS apenas quando ele informa uma amostra recente do PID do jogo reconhecido. Se o servidor estiver fechado, nao detectar o jogo, nao tiver um slot livre ou estiver ocupado, o overlay em janela continua disponivel. O modo Janela permite desativar a integracao RTSS.

A partir da 0.16.15, a ativacao do overlay e salva e restaurada ao abrir. Ao ativar em modo Automatico, o ResetExplode inicia o RTSS instalado se ele nao estiver aberto. Minecraft Java e reconhecido pelo processo java/javaw com titulo Minecraft seguido de versao (incluindo CurseForge); Minecraft.Windows identifica o Bedrock. O launcher e outros aplicativos Java nao sao reconhecidos por essa regra. Em --game-live, o runner de monitoramento verifica o jogo em primeiro plano e o FPS real do RTSS; --show-osd publica um texto temporario e libera o slot ao terminar.

O ResetExplode ocupa exclusivamente um slot proprio, a partir do indice 1. O slot 0 do cliente principal (MSI) e os slots de outros clientes sao preservados. A escrita usa a trava compartilhada do SDK a partir do protocolo 2.14, libera a trava em finally e incrementa dwOSDFrame. O slot proprio e limpo ao desligar, perder o jogo em primeiro plano ou encerrar. Nao modifica perfis, deteccao global, fontes ou coordenadas globais do RTSS.

Posicao, fonte e tamanho do modo RTSS sao configurados no proprio RTSS. Selecao de metricas, ordem e cores por componente sao configuradas no ResetExplode. O grafico de frametime pertence ao modo Janela; a integracao de texto do RTSS nao simula um grafico ausente. FPS/frametime podem vir da amostra real do RTSS. O 1% low usa o coletor ETW do ResetExplode e e omitido quando esse coletor nao fornece uma amostra.

A opcao Usar sensores do MSI e independente do mecanismo de overlay. Quando marcada, reutiliza fontes inequivocas do MAHM para GPU/CPU, com fallback por metrica para os sensores internos. A GPU e associada por identidade, nao pela suposicao de que o indice 0 de dois programas e o mesmo dispositivo. Temperatura/uso agregados da CPU nao sao substituidos por um nucleo isolado. Leituras antigas, nao finitas e sentinelas sao descartadas. Nenhuma dessas leituras externas participa do corte termico de stress.

A tabela MSI exibe os sensores compartilhados com atual/minimo/maximo e permite zerar os extremos locais. A coleta ocorre no maximo uma vez por segundo e reutiliza o buffer de memoria. O MSI precisa estar aberto e os sensores precisam estar habilitados nele. Se ele fechar, os dados antigos nao permanecem no overlay como se fossem atuais.

## Limites e verificacao

O RTSS precisa estar instalado, aberto, com OSD ativo e autorizado a detectar o jogo pelo proprio perfil. A integracao nao contorna bloqueios de jogos ou de anticheat. Sem RTSS, o modo em janela funciona sobre jogos em janela/sem bordas, sujeito ao compositor do Windows.

Testes: dotnet run --project tests/MonitoringTests.csproj -c Release. Cobrem os layouts documentados, identidade de GPU, temperatura agregada, amostras antigas, offsets/contagens invalidos, FLT_MAX, estatisticas do PID correto, reserva de slots, trava concorrente e liberacao do slot.

Provas locais adicionais: --live le os sensores reais do MSI; --rtss-live publica e limpa um texto de teste no RTSS instalado. Esses testes de comunicacao nao substituem a validacao visual em cada jogo/API.

## Personalizacao 0.16.16

Em Aparencia e previa, escolha fonte instalada, tamanho, cor unica ou por linha, negrito, fundo/opacidade, borda, canto, deslocamento e espacamento. A tabela permite alterar nome, cor RGB, escala por linha, formato e X/Y. Posicao livre usa X/Y relativos ao bloco, mantendo o canto e deslocamento globais. Sem posicao livre, as linhas seguem a ordem GPU/VRAM/CPU/RAM e a opcao FPS antes dos sensores.

O formato aceita {label}, {value} e chaves de metricas, como CPU {cpu_temp} | {cpu_load}. Ative as metricas na selecao acima; dados indisponiveis aparecem sem valor real. Um formato vazio oculta aquela linha. O nome javaw e apresentado como Minecraft, com nome manual opcional.

O mesmo modelo monta a previa, o overlay em janela e o texto RTSS. RTSS usa o SDK do OverlayEditor: FNT para fonte base (zoom 1), S para escala por linha, P/L para posicao/camada, E/B para fundo e borda, C para cor. O tamanho do cliente do jogo determina os cantos. Nao escreve perfis globais ou do jogo. Contorno/sombra globais do RTSS permanecem sob controle do RivaTuner e o grafico de frametime permanece no modo Janela. A previa e ilustrativa; rasterizacao pode variar conforme o renderizador e o DPI.

Importar/exportar grava JSON formato ResetExplode.OverlayStyle versao 1, limitado a 64 KB na importacao. Somente propriedades explicitas de aparencia/metricas sao transferidas. API, inicializacao do Windows, atualizacoes, configuracoes de energia e historico ficam fora do arquivo. Perfis invalidos nao sao aplicados; cores, limites, fontes e texto sao normalizados. Texto arbitrario nao injeta tags do RTSS.