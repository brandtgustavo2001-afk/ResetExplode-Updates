# Plano: cinco funções prioritárias do ResetExplode

## Objetivo

Entregar as cinco primeiras melhorias sugeridas, mantendo o aplicativo compilável e com dados confiáveis:

1. diagnóstico automático do PC;
2. overlay mais personalizável;
3. monitoramento avançado de sensores;
4. histórico de desempenho;
5. benchmark automático de jogos.

## Decisões

- Reaproveitar os sensores, overlay e benchmark existentes antes de criar novas camadas.
- Persistir histórico em arquivos JSON locais, com limite de tamanho e escrita atômica.
- Manter proteção térmica e tratar leituras ausentes como desconhecidas, nunca como zero.
- Fazer as alterações incrementais, com testes focados após cada fatia.

## Tarefas e critérios

### Fase 1 — fundação

- [ ] Criar modelos e serviço de diagnóstico com regras explícitas para temperatura, carga, memória, armazenamento e sensores indisponíveis.
- [ ] Exibir um resumo acionável no painel principal, com pontuação e explicação.

### Fase 2 — monitoramento

- [ ] Ampliar a seleção e a apresentação do overlay usando os sensores já validados, preservando atualização ao vivo.
- [ ] Registrar amostras de temperatura, uso, FPS e consumo em histórico local com retenção.

### Fase 3 — benchmark

- [ ] Criar uma sessão de benchmark de jogo com FPS médio, mínimo, máximo, 1% low, 0,1% low e frametime.
- [ ] Exibir e persistir o resultado para comparação futura.

## Verificação

- [ ] Testes focados de modelos, regras, histórico, overlay e benchmark.
- [ ] dotnet build ResetExplode.csproj -c Release.
- [ ] Testes existentes de monitoramento, stress, updater e preview.
- [ ] Conferência final do diff e do estado do repositório.

## Riscos

- Leituras diferentes entre CPU Package, núcleos e sensores externos: manter a origem explícita.
- Jogos que não registram frames: mostrar “aguardando amostras”, sem inventar FPS.
- Histórico crescer indefinidamente: limitar por idade e quantidade.
