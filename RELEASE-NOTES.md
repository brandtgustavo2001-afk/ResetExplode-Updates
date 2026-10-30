0.16.20: nomes de componentes e textos informativos podem ser copiados com botao direito > Copiar texto. Copia o nome completo mesmo quando aparece cortado, incluindo textos formatados. Campos editaveis mantem a copia nativa. Inclui a opcao Fundo transparente da 0.16.19.

0.16.18: selecao de indicadores simplificada em botoes de ligar/desligar para FPS, placa de video, processador, RAM, memoria de video e nome do jogo. A lista detalhada fica em uma secao avancada fechada. Inclui toda a edicao ao vivo e continuidade no Alt+Tab da 0.16.17.

0.16.17: configuracao simples do overlay, com estilos So FPS, Compacto e Letras grandes. Mudancas de fonte, tamanho, fundo, nome e cor aparecem automaticamente, inclusive durante a escolha RGB. Aplicacao em ate aproximadamente 150 ms, com salvamento separado apos a edicao. Opcoes avancadas ficam recolhidas. RTSS mantem o alvo ao usar Alt+Tab e aceita novas configuracoes mesmo quando o jogo pausa os frames; fechar o jogo limpa a sessao. Jogos minimizados ou em tela cheia exclusiva nao podem ser vistos junto do aplicativo: para configurar lado a lado use janela sem bordas. Atualizacoes agora usam um unico botao com acao clara: procurar ou instalar.

0.16.16: editor de aparencia compartilhado entre RTSS, overlay em janela e previa. Fonte instalada, tamanho, negrito, cores por linha com seletor RGB, fundo com opacidade, borda, posicao por canto ou livre por linha, espacamento, separadores, rotulos, FPS antes/depois e nome amigavel do jogo. Formatos com metricas individuais, como CPU {cpu_temp} | {cpu_load}. Importacao/exportacao de estilos JSON sem credenciais ou preferencias globais. Contorno e efeitos globais do RTSS continuam no RivaTuner; o grafico de frametime permanece no modo Janela.

0.16.15: corrige reconhecimento do Minecraft Java (incluindo CurseForge) e Bedrock. Outros aplicativos Java e o launcher nao recebem overlay por essa deteccao. A ativacao do overlay agora e salva entre reinicios; o modo Automatico abre o RTSS instalado quando necessario. O status explica quando esta usando janela e como habilitar RTSS para tela cheia. Mantidos sensores, protecao termica e atualizacoes assinadas.

Monitoramento inspirado no fluxo MSI Afterburner + RTSS: integracao com o compartilhamento oficial de sensores do MSI e publicacao de OSD no RTSS, com fallback para o overlay em janela. Nova tabela de sensores MSI com atual/minimo/maximo e reset dos extremos, fonte de sensores opcional, modo de renderizacao e botao para abrir o RTSS instalado.

O overlay em janela agora usa texto compacto, alinhamento, cores por componente e sombra. No RTSS, posicao/fonte/tamanho continuam sendo ajustados pelo proprio RivaTuner; as metricas, ordem e cores vem do ResetExplode. O RTSS precisa detectar o jogo e ter OSD habilitado. O grafico de frametime permanece no modo Janela.

Nao altera overclock, ventoinhas ou perfis do MSI/RTSS. Sensores externos nao alteram a protecao termica do stress. Mantidos assinatura de atualizacoes, historico de energia, bandeja e configuracoes.

0.16.14: em computadores com duas GPUs de mesmo nome, mantem a coleta interna para evitar atribuir sensores de uma placa a outra. Inclui testes separados por projeto e previa visual reproduzivel. Total: 58 verificacoes automatizadas.

IMPORTANTE PARA QUEM ESTA NA 0.16.8 OU ANTERIOR: a chave antiga de publicacao foi perdida. Execute este instalador UMA VEZ, sem desinstalar antes, para migrar a confianca para o canal v2. A 0.16.8 nao consegue fazer essa migracao sozinha. As proximas versoes assinadas pelo novo canal poderao ser baixadas pelo aplicativo. O manifesto antigo permanece disponivel para evitar erros nos clientes que ainda nao migraram.

O teste de CPU inicia com rampa gradual e 25% por padrao, reduz carga antes do limite e interrompe se o sensor parar de responder. Mantidos overlay configuravel, bandeja, inicio em segundo plano e historico de energia.

Windows 10 1809 ou superior / Windows 11, x64. O instalador inclui .NET 8, Visual C++ x64 e PawnIO. A assinatura do manifesto protege o atualizador; o instalador do aplicativo nao tem certificado Authenticode comercial.
