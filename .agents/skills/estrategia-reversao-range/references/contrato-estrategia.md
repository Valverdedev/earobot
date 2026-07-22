# Contrato tecnico: ReversaoRange

## Fontes canonicas

- `src/Financial.Robot.Worker/Strategy/Estrategias/ReversaoRange.cs`
- `src/Financial.Robot.Worker/Indicators/ParametroParser.cs`
- `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`
- `docs/catalogo-estrategias.md`

## Regra implementada

Exige `niveis` tipados como `suporte` e `resistencia`, alem de indicador RSI calculado. Seleciona o suporte e a resistencia mais proximos pelo `tick.Bid`.

| Parametro | Padrao | Regra |
|---|---:|---|
| `rsiSobrevendaMaximo` | 30 | Compra proxima ao suporte se RSI menor ou igual. |
| `rsiSobrecompraMinimo` | 70 | Venda proxima a resistencia se RSI maior ou igual. |
| `distanciaMaximaDoNivelPontos` | 100 | Distancia absoluta em preco; nao ha conversao automatica de tick. |

Nao exige candle de rejeicao, cruzamento do RSI, VWAP, inclinacao de medias, ATR, largura do range, volume, horario, spread ou confirmacao de retorno para dentro do range. O sinal pode reaparecer enquanto preco e RSI permanecerem extremos. Stop e alvo sao genericos do motor.

## Cenario e risco

Usar apenas em range maduro com extremos respeitados, volatilidade estavel e sem catalisador iminente. Bloquear na transicao para tendencia, em aceitacao fora da faixa, expansao por noticia e niveis proximos demais de spread/stop/custo. RSI extremo pode persistir em rompimento real.

## Configuracao candidata

- Nome obrigatorio: `ReversaoRange`.
- Incluir RSI e niveis manuais tipados, datados e revalidados; validar cada distancia na unidade do simbolo.
- Compensar ausencias da classe com janela de horario, spread/risco, cooldown e maximo de stops no config/motor, sem alegar que a estrategia ja os possui.
- Preservar campos e limites; manter `operar=false` e bloco inativo no candidato.

## Auditoria minima

Para cada trade, registrar mapa do range, numero de testes, RSI, distancia ao nivel, evidencia de tendencia/aceitacao fora da faixa, spread, evento, entrada, SL/TP e reentradas. Classificar trade em rompimento real como fora do regime mesmo se fechar positivo.
