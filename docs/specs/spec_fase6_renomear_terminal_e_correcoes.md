# Spec Fase 6 — Renomear terminal de execução para `activtraders`

## Status (revisado em 2026-07-12)

**As seções 3 e 4 desta spec (drawdown isolado por magic number e correção do `ForcaCestaIndicador`) já foram implementadas** — verificado direto no código antes de finalizar este documento:
- `IGatewayMt5.ObterLucroPrejuizoDiaAsync` / `ObterLucroAbertoAsync` já existem e são chamados em `StrategyEngine.AvaliarEntradaAsync` (item 3, "Drawdown da Estratégia (Individual)").
- `RiskGuard.ValidarDrawdownIsolado` já existe e já é o método usado nesse ponto (não mais `ValidarDrawdownDiario` com a equidade total).
- `ForcaCestaIndicador` já recebe `ILogger`, já implementa `exigeMercadoAvistaAberto` (retorna `null` fora da janela, com log informativo) e já loga `Warning` com a exceção quando um componente falha.

Essas seções ficam mantidas abaixo só como registro do que foi identificado e resolvido. **O único item real ainda pendente desta fase é a Seção 1 (renomear `broker-a` → `activtraders`).**

## Motivação

Decisão do usuário: **todas as operações de execução (todos os ativos, atuais e futuros — cripto, EURUSD, e os que ainda serão configurados: Bra50Aug26, HKInd, GOLD) devem acontecer em um único terminal de execução**, cujo `terminalId` deve ser renomeado de `broker-a` para `activtraders` (nome real da corretora). O terminal `broker-genial` continua como está — somente leitura, dedicado a dados (`ForcaCesta` e afins), nunca envia ordem.

## 1. Renomear `broker-a` → `activtraders`

Ocorrências confirmadas que precisam mudar (busca feita em todo o repositório):

**Configuração:**
- `src/Financial.Robot.Worker/appsettings.json` — seção `Terminals`, campo `TerminalId: "broker-a"` → `"activtraders"`.

**Configs de símbolo (`terminalId` no JSON):**
- `config/BTCUSD.config.json`
- `config/BTCETH.config.json`
- `config/BTCLTC.config.json`
- `config/ADAUSD.config.json`
- `config/EURUSD.config.json`

**Código com fallback hardcoded (⚠️ atenção especial aqui):**
- `src/Financial.Robot.Application/Services/ServicoModoTeste.cs`, linha ~31:
  ```csharp
  private IGatewayMt5 _gateway => _connectionManager.GetClient(_connectionManager.GetConnectedTerminalIds().FirstOrDefault() ?? "broker-a");
  ```
- `src/Financial.Robot.Application/Services/ServicoConexaoMt5.cs`, linha ~18: mesmo padrão.

Trocar o literal `"broker-a"` por `"activtraders"` nos dois arquivos. **Ideal**: extrair esse valor padrão para uma constante única (ex.: `TerminalDefaults.PrincipalId`) em vez de repetir o literal em dois lugares — evita esse mesmo tipo de esquecimento numa próxima renomeação.

**Não mexer** (fora do escopo, referências legítimas de log histórico ou documentação já arquivada):
- `src/Financial.Robot.Worker/logs/financial-robot-20260712.log` — log histórico, não editar.
- `src/Financial.Robot.Worker/bin/Debug/net8.0/appsettings.json` e `tests/.../bin/Debug/net8.0/appsettings.json` — cópias de build, serão regeradas automaticamente no próximo `dotnet build`, não editar manualmente.
- `docs/specs/prompt_fix_stops_minimos_e_volume_adausd.md` e `docs/specs/spec_fase2_config_conexao_dados.md` — specs já arquivadas, documentam decisões históricas com o nome antigo; não precisam ser reescritas, mas pode-se adicionar uma nota de rodapé "terminal renomeado para activtraders em [data]" se quiser manter rastreabilidade.

**Config de teste** (`config/test-mode.config.json`) — conferido: não referencia `broker-a` nem `terminalId` (usa `mtApi.host`/`mtApi.port` diretamente, porta 8228). Nada a alterar aqui.

## 2. Terminal único de execução — implicação para os configs futuros

Os configs ainda não criados (`Bra50Aug26.config.json`, `HKInd.config.json`, `GOLD.config.json`) devem usar `"terminalId": "activtraders"` diretamente desde a criação — não há necessidade de um segundo terminal de execução. `broker-genial` permanece exclusivamente como fonte de dados via `indicadoresMultiFonte`.

## 3. Correção: Drawdown por estratégia não isolado por magic number

**Problema identificado em `StrategyEngine.AvaliarEntradaAsync`** (comentário já presente no código reconhece a limitação):

```csharp
// 3. Drawdown da Estratégia (Individual)
if (_estrategiaConfig.GestaoDeRisco?.DrawdownDiarioMaximoPercent is double drawdownLimit)
{
    // Nota: Drawdown aqui ainda é avaliado sobre a equidade total da conta,
    // um cálculo mais estrito exigiria somar apenas o lucro/prejuízo dos magics da estratégia.
    if (!_riskGuard.ValidarDrawdownDiario(drawdownLimit, _saldoInicialDia, conta.Equidade, brokerSymbol, $"Estratégia: {_estrategiaConfig.Nome}"))
        return;
}
```

O limite de drawdown "individual" de cada estratégia hoje é validado contra a **equidade total da conta** — não contra o resultado daquele magic number especificamente. Isso quebra o propósito central da Fase 5 (comparar estratégias isoladamente): se a `CruzamentoEma` está perdendo mas a `PriceActionSuporteResistencia` está ganhando mais do que isso, a conta como um todo pode parecer saudável e a trava individual da `CruzamentoEma` nunca dispara — mesmo ela estando, na prática, estourando o limite que deveria ter.

### Correção proposta

1. Adicionar um método no `IGatewayMt5` (ou reaproveitar `ObterTicketsPosicoesAbertasAsync` já filtrado por magic + uma nova consulta de histórico de deals do dia) para calcular o **lucro/prejuízo realizado do dia, filtrado por magic number**. Como o `IGatewayMt5` não tem hoje um método de histórico de deals, será necessário adicionar um, ex.:
   ```csharp
   Task<double> ObterLucroPrejuizoDiaAsync(string simbolo, long magicNumber, DateTime inicioDia, CancellationToken ct = default);
   ```
2. Somar a esse valor o P&L **não realizado** das posições abertas daquele magic (via `ObterTicketsPosicoesAbertasAsync` já filtrado, cruzando com o preço atual).
3. `RiskGuard.ValidarDrawdownDiario` (ou uma nova sobrecarga) passa a receber esse P&L isolado em vez da equidade total da conta, calculando o drawdown percentual **daquele magic number sobre o saldo inicial do dia atribuído proporcionalmente à estratégia** (ou sobre um "saldo alocado" configurável por estratégia, se quiser ir além — mas o mínimo aceitável é isolar o P&L, mesmo que a base de cálculo do percentual continue simples).
4. O drawdown **agregado** (`DrawdownDiarioMaximoAgregadoPercent`, item 1 do `AvaliarEntradaAsync`) continua correto como está — esse já é intencionalmente sobre a conta inteira, não precisa mudar.

### Critério de aceite
- [ ] Duas estratégias no mesmo símbolo, uma no lucro e outra estourando seu próprio limite individual de drawdown, resultam na segunda sendo bloqueada mesmo com a conta no total positiva (validável com posições/deals simulados ou reais na demo, comparando magic a magic).

## 4. Correção: `ForcaCestaIndicador` sem filtro de horário de mercado à vista + log silencioso

**Problema 1 — falta o parâmetro `exigeMercadoAvistaAberto`** especificado na Fase 4 (`spec_fase4_indicadores_personalizados.md`, seção 3.1). Hoje o indicador sempre calcula o score, mesmo fora do horário em que as ações-componente (ex.: VALE3/PETR4/ITUB4/BBDC4 para o Bra50) têm candle novo — isso é especialmente crítico na janela 9h-10h BRT do Bra50 (futuro já aberto, ações à vista ainda não), quando o score estaria refletindo o fechamento do dia anterior sem nenhum aviso.

### Correção proposta

```csharp
var exigeMercado = ParametroParser.ObterJanelaHorario(parametros, "exigeMercadoAvistaAberto"); // (TimeSpan? Inicio, TimeSpan? Fim)?
if (exigeMercado.HasValue && !DentroDaJanela(DateTime.UtcNow, exigeMercado.Value))
{
    return new ResultadoIndicador(DateTime.UtcNow, null); // fora da janela — sem informação confiável
}
```

Precisa de um helper `ParametroParser.ObterJanelaHorario` (ou reaproveitar o que já existe para `janelaHorarioPermitido` do `RiskGuard`, se a estrutura for compatível) e considerar fuso horário (a janela é em horário local do mercado, ex.: BRT — cuidado com `DateTime.UtcNow` vs. horário local, mesmo cuidado que já existe em `RiskGuard.ValidarJanelaHorario`).

**Problema 2 — falha de componente é engolida silenciosamente**:

```csharp
catch
{
    // Ignora falha de um símbolo e continua para os demais
}
```

A spec original pedia log como `Warning` quando um componente falha (símbolo não subscrito, terminal fora do ar), justamente para dar visibilidade de que o score pode estar incompleto. Hoje isso falha silenciosamente — se 3 dos 4 componentes falharem, o score ainda "funciona" (calcula sobre 1 componente só) sem nenhum sinal de alerta no log. Precisa injetar um `ILogger` no `ForcaCestaIndicador` (hoje não tem nenhum) e logar `Warning` no catch, incluindo qual símbolo falhou e a exceção.

### Critério de aceite
- [ ] `ForcaCestaIndicador` retorna `null` fora da janela configurada em `exigeMercadoAvistaAberto`, quando o parâmetro estiver presente no config.
- [ ] Falha de componente individual gera log `Warning` explícito com o símbolo e o motivo, não fica silenciosa.
- [ ] Configs antigos (BTCUSD etc.) que não usam `indicadoresMultiFonte` continuam funcionando sem nenhuma mudança de comportamento — o parâmetro é opcional.

## Ordem de implementação sugerida

1. Renomear `broker-a` → `activtraders` (appsettings, 5 configs, 2 arquivos de código com fallback hardcoded) — único item real pendente desta fase. Validar que o robô ainda conecta e opera normalmente nos ativos já em 