# Feature — Alvo Global de Posições Abertas: Multi-Terminal + Valor Absoluto

> Spec de mudança estrutural (não é bugfix). Motivação: hoje `AlvoGlobalPosicoesAbertas` só cobre o terminal `activtraders` (percentual sobre o saldo). O terminal `genial` (WINQ26/BITN26/PETR4, saldo real de ~R$1.000.200) não tem NENHUM alvo global de lucro/perda configurado — flagrado como pendência em várias notas de config ao longo do dia 13/07/2026. Além disso, percentual sobre o saldo não faz sentido para o `genial`: 1% de R$1.000.200 = R$10.002, um valor completamente desproporcional ao tamanho real das posições operadas ali (1-3 mini-contratos, lucro/prejuízo por trade na casa de R$10-80). Por isso o pedido do usuário foi criar um modo "valor absoluto" (R$/USD fixo, não percentual), com um alvo por terminal/corretora.

## Estado atual (antes da mudança)

**Config (`appsettings.json`, `src/Financial.Robot.Worker/appsettings.json` e `publish/appsettings.json`):**
```json
"AlvoGlobalPosicoesAbertas": {
  "Ativo": true,
  "TerminalId": "activtraders",
  "MetaLucroGlobalPercent": 1.0,
  "StopPerdaGlobalPercent": 0.5,
  "IntervaloVerificacaoSegundos": 10
}
```
Objeto único, um só terminal, sempre percentual sobre o saldo (`Balance`, não `Equity`).

**Código:**
- `src/Financial.Robot.Application/Configuracoes/ConfiguracaoAlvoGlobalPosicoesAbertas.cs` — POCO simples (`Ativo`, `TerminalId` string única, `MetaLucroGlobalPercent`/`StopPerdaGlobalPercent` como `double?`, `IntervaloVerificacaoSegundos`). Sem campo de unidade, sem suporte a lista.
- `Program.cs` (linhas ~57-58): `builder.Services.Configure<ConfiguracaoAlvoGlobalPosicoesAbertas>(builder.Configuration.GetSection("AlvoGlobalPosicoesAbertas"))` — bind de objeto único via `IOptionsMonitor<T>` (hot-reload).
- `src/Financial.Robot.Worker/Risk/AlvoGlobalPosicoesAbertasService.cs`, classe `AlvoGlobalPosicoesAbertasService : BackgroundService` (registrado via `AddHostedService` em `Program.cs` linha ~97). `ExecuteAsync` roda um único loop com `Task.Delay(intervalo)`, lendo `_opcoes.CurrentValue` a cada ciclo (config única). `ProcessarAsync(cfg, ct)`: pega o gateway via `_connectionManager.GetClient(cfg.TerminalId)`, soma o lucro flutuante de todas as estratégias ativas do terminal (via `ConfigWatcherService.GetActiveConfigs()` filtrado por `TerminalId`), calcula `pnlPercent = pnlFlutuanteTotal / conta.Saldo * 100.0`, compara contra `MetaLucroGlobalPercent`/`StopPerdaGlobalPercent`. Fecha via `FecharTodasAsync`: para cada (symbol, magic) com posição aberta, busca tickets via `gateway.ObterTicketsPosicoesAbertasAsync` e fecha individualmente via `IServicoExecucao.FecharPosicaoAsync`.
- **Arquitetura atual é 100% single-terminal** — não há como registrar uma segunda instância do serviço sem duplicar toda a classe, porque a config é um objeto singular, não uma lista.

## Mudança proposta

### 1. Config — transformar em lista, com unidade configurável por item

```json
"AlvoGlobalPosicoesAbertas": [
  {
    "TerminalId": "activtraders",
    "Ativo": true,
    "UnidadeAlvo": "percentual",
    "MetaLucroGlobalPercent": 1.0,
    "StopPerdaGlobalPercent": 0.5,
    "MetaLucroGlobalAbsoluto": null,
    "StopPerdaGlobalAbsoluto": null,
    "IntervaloVerificacaoSegundos": 10
  },
  {
    "TerminalId": "genial",
    "Ativo": true,
    "UnidadeAlvo": "absoluto",
    "MetaLucroGlobalPercent": null,
    "StopPerdaGlobalPercent": null,
    "MetaLucroGlobalAbsoluto": 200.0,
    "StopPerdaGlobalAbsoluto": 100.0,
    "IntervaloVerificacaoSegundos": 10
  }
]
```

Escolhas de design:
- **Lista, um item por terminal** (opção escolhida pelo usuário entre duas alternativas apresentadas — a outra era manter `AlvoGlobalPosicoesAbertas` percentual como está e criar uma seção paralela `AlvoGlobalPosicoesAbertasAbsoluto` independente; rejeitada por duplicar lógica em dois lugares do código).
- **Campo `UnidadeAlvo`** (`"percentual"` | `"absoluto"`), mesmo padrão já usado em `SymbolConfig.saida.unidadeDistancia` (`"precoAbsoluto"` etc.) — mantém consistência com o resto do schema do projeto.
- Ambos os pares de campos (`...Percent` e `...Absoluto`) ficam presentes no schema para os dois modos, com o par não usado como `null` — evita branching de schema por unidade, só o serviço decide qual par ler baseado em `UnidadeAlvo`.
- `activtraders` mantém `percentual` (já funciona bem, saldo pequeno e alinhado ao risco real das posições ali). `genial` passa a usar `absoluto` (resolve o problema de percentual sobre R$1M ser desproporcional ao risco real).

### 2. Valores absolutos propostos para `genial` — PENDENTE DE CONFIRMAÇÃO DO USUÁRIO

Baseado no P&L real observado hoje (13/07/2026) só no WINQ26 (uma fase de +222, outra de -70, saldo do dia +152, fora BITN26/PETR4 que teriam contribuição adicional): proponho como ponto de partida `MetaLucroGlobalAbsoluto: 200.0` e `StopPerdaGlobalAbsoluto: 100.0` (em BRL) para o terminal `genial`, cobrindo a soma de todas as posições abertas simultaneamente nesse terminal (WINQ26 + BITN26 + PETR4). Estes são valores de partida sugeridos a partir dos dados de hoje, não validados — recomenda-se o usuário revisar/ajustar antes do primeiro deploy, especialmente porque o volume de trades tende a variar dia a dia.

Para `activtraders`, manter os valores percentuais atuais (1.0% / 0.5% sobre saldo ~US$993 = ~US$9.93 / ~US$4.97) — já em produção e funcionando, sem necessidade de mudança nesta spec.

### 3. Código — mudanças necessárias

**`ConfiguracaoAlvoGlobalPosicoesAbertas.cs`:**
- Adicionar enum `UnidadeAlvoGlobal { Percentual, Absoluto }` (ou string com validação, seguindo o padrão de outros enums de unidade no domínio).
- Adicionar campo `UnidadeAlvo` (enum/string).
- Adicionar `MetaLucroGlobalAbsoluto` e `StopPerdaGlobalAbsoluto` (`double?`), paralelos aos campos percentuais já existentes.

**`Program.cs`:**
- Trocar `Configure<ConfiguracaoAlvoGlobalPosicoesAbertas>` (objeto único) por bind de `List<ConfiguracaoAlvoGlobalPosicoesAbertas>` — usar `Configure<List<ConfiguracaoAlvoGlobalPosicoesAbertas>>(section)` ou um wrapper (`ConfiguracaoAlvoGlobalPosicoesAbertasRoot { Itens: List<...> }`) dependendo de como `IConfiguration.Bind` lida com array na raiz da seção — validar qual abordagem o `Microsoft.Extensions.Configuration` binder aceita mais limpo (arrays JSON bindam bem em `List<T>` diretamente na maioria dos casos, mas vale confirmar com teste rápido).

**`AlvoGlobalPosicoesAbertasService.cs`:**
- `ExecuteAsync` precisa iterar a lista a cada ciclo (um `ProcessarAsync` por item ativo da lista, em vez de assumir uma config única) — pode ser sequencial (loop simples) ou paralelo (`Task.WhenAll`) por terminal; sequencial é mais simples e seguro dado que cada terminal já é uma chamada de rede separada ao gateway, sem necessidade real de paralelismo para poucos terminais (hoje 2).
- `ProcessarAsync(cfg, ct)`: adicionar branch por `cfg.UnidadeAlvo`:
  - `Percentual` (comportamento atual, inalterado): `pnlPercent = pnlFlutuanteTotal / conta.Saldo * 100.0`, compara contra `MetaLucroGlobalPercent`/`StopPerdaGlobalPercent`.
  - `Absoluto` (novo): comparar `pnlFlutuanteTotal` (já em moeda, sem dividir pelo saldo) diretamente contra `MetaLucroGlobalAbsoluto`/`StopPerdaGlobalAbsoluto`. Mesma lógica de fechamento (`FecharTodasAsync`) reaproveitada sem mudança — só a condição de disparo muda.
- Sem mudança nenhuma necessária em `FecharTodasAsync` (já é agnóstico de unidade, só fecha tudo do terminal quando chamado).

## Fora de escopo

- Não muda a lógica de fechamento de posições (`FecharTodasAsync`), só a condição de disparo.
- Não adiciona suporte a `Equity` em vez de `Saldo` no modo percentual (mantém o comportamento atual nesse ponto).
- Não adiciona um terceiro modo (ex.: percentual sobre margem usada) — só percentual (já existe) e absoluto (novo).

## Risco de migração — IMPORTANTE

A config atual (`appsettings.json`) usa objeto único; o código atual (`Configure<ConfiguracaoAlvoGlobalPosicoesAbertas>`) espera objeto único. **Mudar o `appsettings.json` para o formato de lista ANTES de o código ser atualizado quebra o bind da seção** (o serviço passaria a rodar com config vazia/default ou falharia o bind, dependendo do comportamento do `Microsoft.Extensions.Configuration` binder para objeto-vs-array incompatível) — na prática, isso desligaria silenciosamente a proteção de alvo global do `activtraders`, que hoje está ativa em produção. **Recomendação: só migrar o `appsettings.json` para o novo formato DEPOIS que o código (`ConfiguracaoAlvoGlobalPosicoesAbertas.cs`, `Program.cs`, `AlvoGlobalPosicoesAbertasService.cs`) já estiver deployado com suporte à lista.** Não editei o `appsettings.json` real ainda por esse motivo — esta spec descreve o formato-alvo para quando o código estiver pronto.

## Teste recomendado

1. Após o deploy do código: validar que `activtraders` continua dedisparando a meta/stop percentual exatamente como antes (regressão).
2. Validar que `genial` passa a fechar todas as posições abertas (WINQ26 + BITN26 + PETR4 simultaneamente, se houver) quando a soma do P&L flutuante do terminal atingir `MetaLucroGlobalAbsoluto` (200.0) ou `StopPerdaGlobalAbsoluto` (-100.0, ou seja, quando o P&L flutuante ficar <= -100.0).
3. Confirmar no log que `FecharTodasAsync` é chamado com o `TerminalId` correto em cada caso, e que não há mistura de posições entre terminais.
