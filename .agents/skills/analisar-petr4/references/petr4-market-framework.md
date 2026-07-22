# Framework comportamental de PETR4

## Identidade do ativo

`PETR4` e a acao preferencial da Petrobras negociada na B3. Confirmar no terminal `genial` se o simbolo exato e negociavel, pois corretoras podem usar sufixos, lotes fracionarios ou aliases.

Validar tick size, digits, lote minimo, lote step, stops level, moeda, sessao, leiloes, proventos, eventos corporativos, ajustes e disponibilidade de historico. PETR4 nao e fungivel com PETR3, ADR `PBR` ou futuros/CFDs; comparar por retorno percentual sincronizado.

## Matriz de influencia

| Fator | Canal tipico | Pode favorecer PETR4 | Pode pressionar PETR4 | Armadilha |
|---|---|---|---|---|
| Brent/WTI | receita, margem, reservas | petroleo firme com governanca estavel | queda forte ou choque de demanda | petroleo alto pode pressionar combustiveis/inflacao |
| USDBRL | receita dolarizada, custo, fluxo | dolar firme pode elevar receita em BRL | dolar alto por risco local abre DI e reduz multiplo | efeito depende do regime |
| Combustiveis | margem e risco politico | paridade crivel e reajustes previsiveis | intervencao, subsidio ou represamento | fala politica sem ato formal pode reverter |
| Dividendos | retorno ao acionista | payout previsivel e caixa forte | retencao para capex/fiscal | dividendo extraordinario pode ser unico |
| Capex/producao | crescimento e risco de execucao | producao crescente, lifting cost controlado | capex alto, atraso, acidentes | crescimento sem retorno destroi valor |
| Governanca estatal | desconto de risco | autonomia e comunicacao clara | troca de comando/interferencia | narrativa eleitoral nao basta |
| Ibovespa/fluxo | alocacao Brasil | entrada estrangeira e bolsa forte | aversao a Brasil | PETR4 pode divergir por petroleo |
| DI/Selic/fiscal | desconto e risco pais | fechamento da curva | abertura por fiscal/inflacao | juros menores por recessao podem vir com bolsa fraca |
| Eleicao/regulacao | risco de politica publica | previsibilidade institucional | proposta intervencionista | pesquisa isolada nao prova tendencia |

Tratar direcoes como hipoteses condicionais. Medir reacao observada e registrar contradicoes.

## Petrobras e fontes prioritarias

| Tema | Fonte primaria |
|---|---|
| Comunicados, resultados, proventos | Petrobras RI: https://www.investidorpetrobras.com.br/ |
| Fatos relevantes e formularios | CVM: https://www.gov.br/cvm/ |
| Negociacao, calendario, acoes | B3: https://www.b3.com.br/ |
| Petroleo e combustiveis | EIA: https://www.eia.gov/ e ANP: https://www.gov.br/anp/ |
| Selic, Focus, cambio | BCB: https://www.bcb.gov.br/ |
| Inflacao, atividade | IBGE: https://www.ibge.gov.br/ |
| Fiscal | Tesouro: https://www.gov.br/tesouronacional/ |
| Governo e falas | Planalto, Ministerio da Fazenda, MME |

Usar Reuters/AP para tempo real. Nao usar snippet, agregador, blog promocional ou perfil nao verificado como evidencia final.

## Metricas tecnicas

```text
mid = (bid + ask) / 2
spread_abs = ask - bid
spread_points = spread_abs / point_size
spread_atr = spread_abs / ATR14
atr_percent = ATR14 / last_close * 100
gap_abs = open_atual - close_anterior
gap_atr = abs(gap_abs) / ATR14_anterior
retorno_janela = (close_final / close_inicial - 1) * 100
```

Sincronizar timestamps ao comparar PETR4, PETR3, PBR, Brent, USDBRL e Ibovespa. Calcular correlacao de retornos, nunca de niveis. Correlacao nao prova causalidade.

## Bloqueios de confianca

Bloquear promocao por simbolo nao confirmado; stale/gaps anormais; spread alto frente ao ATR; evento corporativo iminente; fato relevante sem absorcao de preco; noticia material sem fonte; falha no extrator; lote/custo nao validado; estrategia incompativel; ou hot reload inseguro por posicao/conflito.
