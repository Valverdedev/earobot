# Framework comportamental do WINQ/Bra50

## Identidade do mercado

`WINQ26` no terminal `genial` e `Bra50Aug26` no terminal `activtraders` sao instrumentos correlatos ao mercado acionario brasileiro. Nao sao fungiveis sem verificacao. Um pode refletir futuro B3; o outro pode incluir ajustes de CFD, horario estendido, financiamento, spread proprio e convencao de preco diferente.

Confirmar contrato, vencimento, horario, calendario, tick size, digits, volume, stops level, moeda, multiplicador, valor do ponto, ajustes e rollover. Comparar retorno percentual sincronizado antes de comparar nivel. Sem relacao de escala documentada, nao calcular basis absoluto.

## Matriz de influencia

| Fator | Canal tipico | Pode favorecer | Pode pressionar | Armadilha |
|---|---|---|---|---|
| USDBRL/WDO | fluxo, inflacao, exportadoras | real forte por entrada de capital | real fraco por aversao/fiscal | dolar alto pode ajudar exportadoras |
| DI/Selic | desconto, credito, fiscal | fechamento crivel da curva | abertura por fiscal/inflacao | juros menores por recessao podem vir com bolsa fraca |
| DXY/UST | liquidez global | DXY/yields menores | DXY/yields maiores | choque global pode dominar fatores locais |
| Minério/China | Vale e siderurgia | minério/China firmes | desaceleracao/minério fraco | peso de Vale muda; validar carteira |
| Brent/Petrobras | lucro, combustiveis, inflacao | petróleo firme e governanca previsivel | queda ou intervencao percebida | petróleo alto pode abrir DI |
| Bancos | credito, inadimplencia, curva | credito saudavel e menor risco | fiscal, inadimplencia, regulacao | curva inclinada nao e sempre positiva |
| S&P/Nasdaq/VIX | risco global | futuros fortes/VIX menor | sell-off/VIX maior | noticia local pode desacoplar |
| Fiscal | premio e juros longos | meta crivel | gasto/receita incertos | anuncio sem execucao pode reverter |
| Eleicao | politica economica esperada | previsibilidade institucional/fiscal | incerteza/proposta inflacionaria | pesquisa isolada nao prova tendencia |
| Tarifas EUA | exportacoes, FX, fiscal | acordo/isencoes | tarifa ampla/retaliacao | setores reagem de forma diferente |

Tratar as direcoes como hipoteses condicionais. Medir reacao observada e registrar contradicoes.

## Acoes e composicao

Consultar a carteira teorica oficial vigente do Ibovespa e registrar a data. Selecionar componentes por peso oficial e sensibilidade ao tema, nao por lista fixa.

Cobrir quando relevantes: Vale/mineracao; Petrobras/petroleo; Itau, Bradesco, Banco do Brasil, Santander e BTG; utilities/estatais; varejo/construcao; frigorificos, papel/celulose, aeronaves e exportadores afetados pelo escopo tarifario real.

O `ForcaCesta` do robo pode usar pesos iguais por desenho. Rotular como parametros do indicador, nunca como composicao oficial. Comparar com versao ponderada pela carteira oficial quando possivel.

```text
retorno_i = (preco_atual_i / preco_referencia_i - 1) * 100
contribuicao_aprox_i = peso_oficial_i * retorno_i
forca_cesta = soma(peso_modelo_i * score_i) / soma(peso_modelo_i)
```

Nao chamar `contribuicao_aprox_i` de contribuicao oficial sem divisor e metodologia oficiais.

## Cambio e juros

Analisar USDBRL/WDO, DXY, Treasury 2Y/10Y, DI curto/intermediario/longo, Selic, Copom, Focus, IPCA, atividade, emprego, fiscal e fluxo estrangeiro B3 quando disponivel.

Classificar o dolar por risco local/fiscal, choque global, termos de troca, fluxo/rollover ou evento eleitoral/tarifario. Nao assumir `dolar sobe = WIN cai` sem identificar regime e composicao.

## Protocolo eleitoral

### Pesquisas

Usar PesqEle/TSE. Coletar codigo, instituto, contratante, financiador, metodologia, modo, campo, divulgacao, universo, amostra, margem, nivel de confianca, cenarios e pesquisa anterior comparavel.

Nao comparar diretamente telefone, presencial e painel digital. Nao tratar oscilacao dentro da margem como mudanca comprovada. Separar primeiro/segundo turno, rejeicao e avaliacao; nacional e estadual; nome oficial, pre-candidato ou apenas testado.

### Falas e propostas

Monitorar Lula e Flavio Bolsonaro enquanto materialmente relevantes e incluir outros nomes que ganhem relevancia em pesquisas, convencoes ou aliancas.

Classificar falas em fiscal/regra de gasto; impostos/subsidios/credito; BCB/juros; Petrobras/bancos publicos/estatais; privatizacao/regulacao; comercio/tarifas/EUA/China; governanca e risco institucional.

Preferir transcricao/video oficial, Planalto, Senado, Camara, partido ou conta oficial confirmada. Usar Reuters/AP para contexto e reacao. Parafrasear fielmente e evitar recortes.

Registrar `fato`, `canal`, `direcao teorica`, `reacao observada em BRL/DI/WIN`, `confianca` e `invalidacao`.

## Protocolo de tarifas

Separar investigacao/ameaca, proposta/escopo, consulta/audiencia, decisao, vigencia, isencoes, negociacao, retaliacao e apoio domestico.

Avaliar parcela de receita/exportacao afetada; balanca/USDBRL/inflacao; Vale, Petrobras, siderurgia, papel/celulose, agro, frigorificos e Embraer conforme o escopo; custo fiscal; cadeias de insumos; efeito eleitoral sem confundir narrativa com impacto observado.

Uma tarifa pode prejudicar exportadores afetados, fortalecer o dolar e beneficiar outros exportadores ao mesmo tempo. Registrar dispersao setorial.

## Fontes prioritarias

| Tema | Fonte primaria |
|---|---|
| Contratos, carteira e calendario | B3: https://www.b3.com.br/ |
| Pesquisas/calendario | TSE/PesqEle: https://www.tse.jus.br/ e https://pesqele-divulgacao.tse.jus.br/ |
| Selic, Copom, Focus, cambio | BCB: https://www.bcb.gov.br/ |
| Inflacao, emprego, atividade | IBGE: https://www.ibge.gov.br/ |
| Fiscal e divida | Tesouro: https://www.gov.br/tesouronacional/ |
| Comercio exterior | MDIC/Comex Stat: https://www.gov.br/mdic/ |
| Tarifas/Section 301 | USTR: https://ustr.gov/ |
| Governo e falas | Planalto, Senado e Camara |
| Empresas | RI oficial e CVM: https://www.gov.br/cvm/ |
| EUA | Federal Reserve, BLS, BEA e U.S. Treasury |

Usar Reuters/AP para tempo real. Nao usar snippet, agregador, blog promocional ou perfil nao verificado como evidencia final.

## Metricas tecnicas e paridade

```text
mid = (bid + ask) / 2
spread_abs = ask - bid
spread_points = spread_abs / point_size
spread_atr = spread_abs / ATR14
atr_percent = ATR14 / last_close * 100
gap_abs = open_atual - close_anterior
gap_atr = abs(gap_abs) / ATR14_anterior
retorno_janela = (close_final / close_inicial - 1) * 100
corr_rolling = corr(retorno_WINQ, retorno_Bra50, janela_n)
```

Sincronizar timestamps. Calcular correlacao de retornos, nunca de niveis. Reportar janela, timeframe, ausencias e estabilidade. Correlacao nao prova causalidade.

## Janelas de comportamento

Validar horarios diariamente na B3 e brokers. Separar pre-abertura/abertura do futuro, abertura do cash brasileiro, dados BCB/IBGE, abertura de Nova York considerando DST, leiloes, fechamento/ajuste, after-hours, feriados e rollover.

Antes da abertura das acoes, a cesta pode estar stale. Nao usar `ForcaCesta` como confirmacao contemporanea sem ressalva.

## Bloqueios de confianca

Bloquear promocao por contrato vencido/rollover sem plano; divergencia inexplicada WINQ/Bra50; stale/gaps/escalas desconhecidas; pesquisa sem PesqEle; fala sem fonte; evento iminente; pesos desatualizados; exposicao duplicada sem limite; estrategia/unidade nao validadas.
