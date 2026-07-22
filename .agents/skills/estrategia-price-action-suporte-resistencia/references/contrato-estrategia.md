# Contrato tecnico: PriceActionSuporteResistencia

## Fontes canonicas

- `src/Financial.Robot.Worker/Strategy/Estrategias/PriceActionSuporteResistencia.cs`
- `src/Financial.Robot.Worker/Indicators/ParametroParser.cs`
- `src/Financial.Robot.Worker/Strategy/StrategyEngine.cs`
- `docs/catalogo-estrategias.md`

## Regra implementada

`niveis` e obrigatorio. Cada nivel possui `tipo` e `preco`; `tipo=suporte` nao permite compra por rompimento nem venda por rejeicao, e `tipo=resistencia` nao permite venda por rompimento nem compra por rejeicao. Tipos vazios/genericos permitem os dois lados.

Parametros suportados:

| Parametro | Padrao | Efeito |
|---|---:|---|
| `toleranciaRompimentoPreco` ou `toleranciaRompimentoPontos` | 50 | Distancia absoluta de preco; nao ha conversao de tick. |
| `exigeRetesteConfirmado` | `true` | `true` usa rompimento+reteste; `false` usa rejeicao. |
| `candlesConfirmacao` | 2 | Define a janela curta depois do rompimento. |

Modo reteste: usa os ultimos `candlesConfirmacao + 2` candles. O primeiro fecha alem do nivel+tolerancia; os intermediarios nao podem violar o nivel pelo lado contrario alem da tolerancia; o ultimo fecha alem da maxima/minima do candle de rompimento. Compra/venda ainda depende de `comprar`/`vender`.

Modo rejeicao: o ultimo candle deve tocar o nivel dentro da tolerancia. Compra exige candle de alta e pavio inferior maior que 1,5x o corpo; venda exige candle de baixa e pavio superior maior que 1,5x o corpo.

Nao ha filtro interno de spread, volume, ATR, tendencia, timeframe maior, expiracao automatica de nivel ou stop estrutural. A saida e generica do motor.

## Cenario e risco

Reteste e apropriado a aceitacao apos rompimento ou continuacao; rejeicao e apropriada a range maduro, falso rompimento ou pullback alinhado ao contexto maior. Evitar nivel antigo, muitos testes, pavios de noticia, proximidade de varios niveis e rejeicao contra tendencia forte.

## Configuracao candidata

- Nome obrigatorio: `PriceActionSuporteResistencia`.
- Criar blocos e magic numbers separados para reteste e rejeicao; nunca mudar os dois sob o mesmo identificador para medir desempenho.
- Declarar niveis tipados, fonte, horario de mapeamento e unidade de preco validada.
- Preservar saida, risco e campos desconhecidos; candidato deve ter `operar=false` e bloco `ativa=false` por padrao.

## Auditoria minima

Guardar screenshot/candles do nivel, tipo, tolerancia, modo, sequencia completa da janela, direcao habilitada, horario, spread, entrada, SL/TP e desfecho. Um rompimento sem confirmacao final nao e trade aderente.
