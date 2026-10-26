# Monitoramento e overlay - integracao MSI / RTSS

Implementacao independente baseada nos SDKs publicos presentes nas instalacoes locais do MSI Afterburner e do RivaTuner Statistics Server. Nao inclui binarios, drivers, skins, marcas ou codigo de controle de overclock desses aplicativos.

## Fontes consultadas

- MSI Afterburner/SDK/Include/MAHMSharedMemory.h: cabecalho MAHM v2, timestamp Unix de coleta, tamanho/contagem das entradas, identificadores de origem, indice/identidade da GPU e sentinela FLT_MAX.
- MSI Afterburner/SDK/Samples/SharedMemory/MAHMSharedMemorySample: exemplo de consumo do compartilhamento MAHMSharedMemory.
- RivaTuner Statistics Server/SDK/Include/RTSSSharedMemory.h e Samples/SharedMemory/RTSSSharedMemorySample: tabela de aplicativos, campos de FPS/frametime, slots de OSD, texto estendido, contador de quadros e sincronizacao dwBusy.

## Funcionamento

Em Overlay, o modo Automatico usa o RTSS apenas quando ele informa uma amostra recente do PID do jogo reconhecido. Se o servidor estiver fechado, nao detectar o jogo, nao tiver um slot livre ou estiver ocupado, o overlay em janela continua disponivel. O modo Janela permite desativar a integracao RTSS.

O ResetExplode ocupa exclusivamente um slot proprio, a partir do indice 1. O slot 0 do cliente principal (MSI) e os slots de outros clientes sao preservados. A escrita usa a trava compartilhada do SDK a partir do protocolo 2.14, libera a trava em finally e incrementa dwOSDFrame. O slot proprio e limpo ao desligar, perder o jogo em primeiro plano ou encerrar. Nao modifica perfis, deteccao global, fontes ou coordenadas globais do RTSS.

Posicao, fonte e tamanho do modo RTSS sao configurados no proprio RTSS. Selecao de metricas, ordem e cores por componente sao configuradas no ResetExplode. O grafico de frametime pertence ao modo Janela; a integracao de texto do RTSS nao simula um grafico ausente. FPS/frametime podem vir da amostra real do RTSS. O 1% low usa o coletor ETW do ResetExplode e e omitido quando esse coletor nao fornece uma amostra.

A opcao Usar sensores do MSI e independente do mecanismo de overlay. Quando marcada, reutiliza fontes inequivocas do MAHM para GPU/CPU, com fallback por metrica para os sensores internos. A GPU e associada por identidade, nao pela suposicao de que o indice 0 de dois programas e o mesmo dispositivo. Temperatura/uso agregados da CPU nao sao substituidos por um nucleo isolado. Leituras antigas, nao finitas e sentinelas sao descartadas. Nenhuma dessas leituras externas participa do corte termico de stress.

A tabela MSI exibe os sensores compartilhados com atual/minimo/maximo e permite zerar os extremos locais. A coleta ocorre no maximo uma vez por segundo e reutiliza o buffer de memoria. O MSI precisa estar aberto e os sensores precisam estar habilitados nele. Se ele fechar, os dados antigos nao permanecem no overlay como se fossem atuais.

## Limites e verificacao

O RTSS precisa estar instalado, aberto, com OSD ativo e autorizado a detectar o jogo pelo proprio perfil. A integracao nao contorna bloqueios de jogos ou de anticheat. Sem RTSS, o modo em janela funciona sobre jogos em janela/sem bordas, sujeito ao compositor do Windows.

Testes: dotnet run --project tests/MonitoringTests.csproj -c Release. Cobrem os layouts documentados, identidade de GPU, temperatura agregada, amostras antigas, offsets/contagens invalidos, FLT_MAX, estatisticas do PID correto, reserva de slots, trava concorrente e liberacao do slot.

Provas locais adicionais: --live le os sensores reais do MSI; --rtss-live publica e limpa um texto de teste no RTSS instalado. Esses testes de comunicacao nao substituem a validacao visual em cada jogo/API.
