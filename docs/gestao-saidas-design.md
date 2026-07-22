# Design: gestão de saídas — trailing, técnica, percentual e valor bruto

**Data:** 18/07/2026
**Status:** proposta — nenhum código escrito
**Decisões do usuário:** TL = trailing stop; saída percentual nas duas bases (% do preço de entrada E % do saldo da conta); saída por valor bruto (R$) por posição.

## 1. O que já existe hoje (inventário)

`SaidaConfig` ([Financial.Robot.Domain/ValueObjects/SaidaConfig.cs](../src/Financial.Robot.Domain/ValueObjects/SaidaConfig.cs)) + `GerenciadorPosicoesAbertasService`:

| Mecanismo | Campos | Onde executa |
|---|---|---|
| SL/TP fixo | `StopLossPips`/`TakeProfitPips`, `StopLossAtrMultiplo`/`TakeProfitAtrMultiplo`, `UnidadeDistancia` | servidor (na ordem) |
| Encerramento por preço | `EncerrarAtivaAcimaDe`/`AbaixoDe` | monitoramento |
| Trailing stop | `TrailingStopPips`, `TrailingStopAtrMultiplo` | monitoramento (modifica SL) |
| Breakeven | `BreakevenGatilhoAtrMultiplo`, `BreakevenBufferPips`, `BreakEvenAposParcial` | monitoramento |
| Saídas parciais | `SaidasParciais` (lista de `SaidaParcialConfig`) | monitoramento |
| Proteções | `SlMinimoSobreSpread`, `CooldownAposFechamentoSegundos` | motor |
| Alvo global da conta | `AlvoGlobalPosicoesAbertas` (appsettings, Fase 9) | serviço próprio |

**O que falta (este design):** saída por condição técnica, saída por % (preço/conta), saída por R$ por posição, e trailing percentual.

## 2. Arquitetura — dois trilhos, fronteira preservada

A fronteira do projeto DSL continua valendo: **o que decide "fechar" pode ser dinâmico; o que executa o fechamento é sempre o motor compilado.**

- **Trilho A — saídas paramétricas (compiladas):** percentual, valor bruto e trailing são números de configuração; entram no `SaidaConfig` e são monitorados pelo `GerenciadorPosicoesAbertasService`, como breakeven/trailing já são hoje.
- **Trilho B — saída técnica (DSL):** condições de leitura de candle/indicador para encerrar posição aberta; entram no `.estrategia.json` num novo bloco `saida`, avaliado a cada candle fechado, reutilizando TODO o vocabulário existente (operandos, operadores, padrões, validação e rastro auditável).

## 3. Trilho A — novos campos do `SaidaConfig`

```csharp
// % sobre o preço de entrada da posição (ex.: 0.5 = meio por cento)
decimal? TakeProfitPercentualPreco,
decimal? StopLossPercentualPreco,
// % sobre o saldo da conta no momento da avaliação (lucro/prejuízo da posição em R$ vs saldo)
decimal? TakeProfitPercentualConta,
decimal? StopLossPercentualConta,
// R$ absolutos de lucro/prejuízo da posição (profit que o MT5 já reporta)
decimal? TakeProfitValorBruto,
decimal? StopLossValorBruto,
// trailing em % do preço (junta-se ao TrailingStopPips/AtrMultiplo existentes)
decimal? TrailingStopPercentualPreco
```

Semântica de execução:

1. **`*PercentualPreco`** é convertível em preço no envio da ordem ⇒ vira **SL/TP no servidor** (protege mesmo com o robô offline). Mutuamente exclusivo, por perna, com `*Pips` e `*AtrMultiplo` — o validador de config rejeita mais de uma fonte de SL ou de TP de servidor.
2. **`*PercentualConta`** e **`*ValorBruto`** dependem do `profit` corrente da posição ⇒ são **monitorados** pelo Gerenciador (fecham a mercado via gateway, como `EncerrarAtivaAcimaDe` faz). Podem coexistir entre si e com o SL/TP de servidor: **a primeira condição atingida fecha a posição**.
3. **Trailing**: uma única fonte ativa por vez (pips OU ATR OU percentual) — validador rejeita combinação.
4. A regra existente continua: **entrada sem nenhum SL calculável é bloqueada**. As saídas de monitoramento nunca substituem o SL de servidor; são camadas adicionais.
5. `PercentualConta` exige leitura de saldo (`get_account_info` via gateway) com cache curto (por ciclo de monitoramento), nunca por tick.

## 4. Trilho B — bloco `saida` na DSL

Novo bloco de topo no `.estrategia.json` (opcional, ao lado de `compra`/`venda`):

```json
"saida": {
  "compra": {
    "todas": [
      { "op": "cruzouAbaixo", "a": "ema(9)", "b": "ema(21)" },
      { "padrao": "rejeicaoVendedora", "candle": 0, "multiploPavio": 2.0 }
    ]
  },
  "venda": { "qualquer": [] }
}
```

`saida.compra` = condições para ENCERRAR uma posição COMPRADA; `saida.venda` = para encerrar uma VENDIDA.

Regras:

- Avaliado **somente quando há posição aberta** da estratégia (magic number), a cada candle fechado, pelo Gerenciador — nova interface `IEstrategiaSaida` que a `EstrategiaInterpretada` implementa (estratégias compiladas não são afetadas).
- Vocabulário idêntico ao de entrada (operandos, operadores, padrões, `todas`/`qualquer`), mesma validação na carga, mesmo rastro de auditoria (`saida/compra: cruzouAbaixo OK | rejeicaoVendedora FALHOU`).
- **Novos operandos de posição**, válidos apenas dentro do bloco `saida` (validador rejeita fora dele):
  - `posicao.precoEntrada`
  - `posicao.lucroBruto` (R$, profit do MT5)
  - `posicao.lucroPercentualPreco` (% sobre preço de entrada, com sinal)
- Refs (`ref.*`) de padrões da entrada **não** existem na saída — a avaliação é independente (interpretação restritiva; se um dia fizer falta, vira extensão).
- Condição verdadeira ⇒ Gerenciador fecha a posição **a mercado**, com o motivo técnico no log. Sem estado entre candles.

## 5. Precedência e interação entre todas as saídas

Ordem de avaliação no ciclo do Gerenciador (a primeira que disparar encerra e interrompe o resto):

1. SL/TP de servidor (executa na corretora, fora do robô);
2. `EncerrarAtivaAcimaDe/AbaixoDe` (existente);
3. Valor bruto (R$) — proteção de perda primeiro, alvo depois;
4. Percentual (conta, depois preço quando monitorado);
5. Saída técnica (DSL);
6. Parciais / breakeven / trailing (não encerram; ajustam).

Racional: proteções objetivas de dinheiro vêm antes de leitura técnica; ajustes que não fecham ficam por último.

## 6. Validação (na carga do config e da definição DSL)

- Máx. uma fonte de SL de servidor e uma de TP de servidor por estratégia (pips | ATR | percentualPreco).
- Máx. uma fonte de trailing (pips | ATR | percentualPreco).
- Valores ≤ 0 rejeitados.
- Bloco `saida` da DSL: mesmas regras estruturais dos blocos de entrada; operandos `posicao.*` proibidos fora de `saida`; **falha permissiva proibida** — parâmetro ausente ⇒ rejeição na carga, nunca "sempre verdadeiro".

## 7. Fases de implementação

**F1 — paramétricas (Trilho A):** campos novos no `SaidaConfig` + conversão `PercentualPreco`→preço no envio + monitoramento de `ValorBruto`/`PercentualConta` + `TrailingStopPercentualPreco` no Gerenciador + validador de config + testes unitários por mecanismo (incluindo precedência).
**F2 — técnica (Trilho B):** interface `IEstrategiaSaida`, bloco `saida` na DSL (parser/validador/avaliador reutilizados), operandos `posicao.*`, integração no ciclo do Gerenciador + testes (incluindo: posição comprada não avalia bloco `venda` e vice-versa).
**F3 — sombra:** como na entrada, período de observação em terminal somente-leitura antes de habilitar em conta real — um bug de saída fecha cedo demais ou não fecha; os dois modos de falha precisam ser vistos em sombra primeiro.
