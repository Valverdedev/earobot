---
name: analisar-winq
description: Analise profissional, comportamental e auditavel de Mini Indice/Ibovespa para este robo, cobrindo WINQ26 no terminal Genial e Bra50Aug26 no terminal ActivTrades. Use ao pesquisar fatores que influenciam WIN/Bra50, incluindo dolar, curva de juros, commodities, acoes de maior peso, exterior, pesquisas e falas eleitorais, tarifas dos EUA, Price Action, estrategia registrada, relatorio auditavel, config candidato e eventual promocao segura para D:\SistemEarobot\config.
---

# Analisar WINQ

## Objetivo

Produzir uma leitura comportamental e operacional rastreavel do Mini Indice/Ibovespa. Tratar `WINQ26` e `Bra50Aug26` como exposicoes relacionadas ao mesmo mercado brasileiro, mas como instrumentos distintos em terminais distintos. Nunca presumir igualdade de preco, escala, tick, volume, sessao, vencimento, spread ou regras de execucao.

Admitir `NAO OPERAR` quando mercado, dados, evento, liquidez, contrato ou codigo nao estiverem validados. Nao tratar a analise como garantia de retorno.

Ler integralmente antes de agir:

- `references/winq-market-framework.md`
- `references/report-template.md`
- `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md`

## Instrumentos e caminhos

| Papel | Terminal | Simbolo | Config ativo |
|---|---|---|---|
| Futuro B3 / referencia local | `genial` | `WINQ26` | `D:\SistemEarobot\config\WINQ26.config.json` |
| CFD/contrato ActivTrades | `activtraders` | `Bra50Aug26` | `D:\SistemEarobot\config\Bra50Aug26.config.json` |

- Relatorios: `D:\SistemEarobot\relatorios\WINQ`
- Preparador: `scripts/Prepare-WinqAnalysis.ps1`
- Extrator: `D:\SistemEarobot\financial.robot\publish-extractor\Financial.Robot.Extractor.exe`
- Catalogo: `D:\SistemEarobot\financial.robot\docs\catalogo-estrategias.md`

O codigo `Q26` e o texto `Aug26` sugerem agosto de 2026, mas confirmar vencimento, ultimo dia de negociacao, rollover e contrato ativo em fonte oficial e nos metadados do broker a cada analise.

## Regras inegociaveis

1. Nao inventar cotacoes, candles, composicao do indice, pesos, pesquisas, falas, noticias, agenda, consenso, realizado ou niveis.
2. Marcar dado nao confirmado como `[NAO CONFIRMADO]` e nao o usar como premissa decisiva.
3. Registrar URL direta, publicado/evento em e acessado em para toda afirmacao material externa.
4. Separar `fato`, `calculo`, `inferencia` e `cenario`.
5. Usar horario ISO 8601 com fuso `America/Sao_Paulo`; incluir UTC em eventos de alto impacto.
6. Consultar a carteira oficial vigente do Ibovespa. Nao tratar pesos historicos ou pesos iguais de `ForcaCesta` como pesos oficiais.
7. Nao afirmar causalidade por correlacao intradiaria. Dolar, juros, commodities e acoes podem mudar de sinal conforme o regime.
8. Tratar pesquisas eleitorais somente com registro PesqEle, metodologia, campo, amostra, margem, contratante e comparacao adequada.
9. Antes das convencoes e do registro, usar `pre-candidato`, `nome testado` ou `ator politico`, nao `candidato oficial`.
10. Monitorar Lula e Flavio Bolsonaro enquanto forem materialmente relevantes, sem limitar a analise a eles e sem inferencia partidaria.
11. Nao transformar fala isolada em sinal. Classificar o canal: fiscal, juros, BCB, estatais, impostos, comercio, tarifas ou governanca.
12. Nao forcar estrategia. `Nenhuma estrategia / operar=false` e valido.
13. Nao aumentar lote, risco, drawdown, posicoes, metas ou permissao de operar sem autorizacao explicita da execucao atual.
14. Nao promover simultaneamente os dois configs apenas por representarem o mesmo subjacente. Isso duplica exposicao.
15. Nao alterar config ativo para concluir a tarefa. A gravacao pode acionar hot reload e ordens.

## Fluxo obrigatorio

### 1. Preparar e extrair

Executar:

```powershell
& "D:\SistemEarobot\financial.robot\.agents\skills\analisar-winq\scripts\Prepare-WinqAnalysis.ps1" -RunExtractor
```

Capturar o objeto retornado. O script prepara um relatorio, duas extracoes e dois candidatos. Se uma extracao falhar, registrar diagnostico e bloquear promocao de ambos; a outra fonte ainda pode apoiar analise degradada.

### 2. Ler estado anterior

Ler integralmente o ultimo relatorio `yyyy-MM-dd_HH-mm-ss_winq.md`, os dois JSONs, os dois configs ativos quando existirem, o catalogo, alertas de rollover e posicoes abertas. Registrar tese anterior, invalidacoes, mudanca em ATR/regime, eventos, hashes e exposicao agregada.

### 3. Validar os instrumentos

Para cada simbolo, conferir terminal, conta, modo, titular, bid/ask, point size, digits, stops level, volume, candle, sessao e posicoes. Confirmar que o contrato esta ativo e negociavel.

Comparar por retorno percentual em janelas sincronizadas, direcao, volatilidade, sessao, gaps, spread/ATR e divergencias. Calcular `basis_percent = (preco_secundario_normalizado / preco_primario_normalizado - 1) * 100` somente apos validar escalas. Sem comprovacao, comparar apenas retornos e rotular o basis `[NAO CALCULADO]`.

### 4. Pesquisar contexto comportamental

Pesquisar fontes atuais e avaliar no minimo:

- Ibovespa e carteira oficial vigente;
- `USDBRL`/WDO, DXY e fluxo para emergentes;
- curva DI, Selic, Copom, Focus, fiscal e inflacao;
- Vale/minerio/China, Petrobras/Brent e bancos relevantes;
- S&P 500, Nasdaq, VIX, Treasury yields e commodities;
- pesquisas eleitorais registradas e mudancas comparaveis;
- falas economicas de Lula, Flavio Bolsonaro e outros nomes relevantes;
- investigacoes, tarifas e decisoes dos EUA que afetem o Brasil;
- agenda brasileira, americana e chinesa;
- sessao B3, abertura do cash, Nova York, feriados e rollover.

Usar fontes primarias primeiro. Reuters/AP podem apoiar tempo real. Post social so vale como fonte primaria da fala quando a conta for confirmada; corroborar consequencias em fonte independente.

### 5. Atualizar ou manter a tese

Refazer integralmente quando nao houver relatorio, a tese tiver mais de 6 horas, houver pesquisa/fala material, decisao tarifaria, Copom/Fed, dado macro, alteracao fiscal, movimento acima de 1 ATR H1, mudanca de regime, abertura de sessao, rollover ou conflito de fontes.

Manter e revalidar somente quando tiver ate 2 horas e nenhum gatilho tiver ocorrido. Entre 2 e 6 horas, justificar conservadoramente. Rotular `mantido`, `atualizado` ou `invalidado`.

### 6. Ler grafico e intermercado

Fazer leitura `D1 -> H4 -> H1 -> M15 -> M5 -> M1` nos dois instrumentos. Calcular metricas do framework e classificar tendencia, range, transicao, impulso, correcao, compressao, expansao, rompimento, reteste e falha.

Construir tabela de confirmacoes/contradicoes entre WIN/Bra50, cambio, juros, acoes pesadas, commodities e exterior. Nao somar sinais de frequencias diferentes sem explicitar timestamp e janela.

Aplicar separadamente as lentes publicas de Al Brooks, Alexandre Wolwacz (Stormer) e Fabricio Lorenz. Nao imitar a voz deles nem atribuir ordem especifica.

### 7. Tratar eleicao e tarifas

Para cada pesquisa, registrar codigo PesqEle, instituto, contratante, metodologia, datas de campo, amostra, margem e comparacao compativel. Oscilacao dentro da margem nao e mudanca comprovada.

Para cada fala, guardar parafrase fiel ou trecho curto, fonte primaria, contexto, data/hora e canal economico. Avaliar reacao observada em BRL, DI e indice antes de atribuir impacto.

Para tarifas, decompor exposicao setorial direta, cambio, inflacao, resposta fiscal, retaliacao, crescimento e premio politico. Separar proposta, audiencia, prazo, decisao e vigencia.

### 8. Montar cenarios

Produzir cenarios altista, baixista e neutro com condicoes, gatilho, invalidacao, objetivos, risco, validade e estrategia candidata ou `nenhuma`.

Se houver evento de alto impacto nos proximos 30 minutos, decisao tarifaria pendente no pregao, pesquisa material recem-divulgada sem absorcao de preco ou fala ao vivo relevante, manter candidatos com `operar=false`. Reavaliar no minimo 15 minutos depois e somente apos normalizacao de spread, liquidez e estrutura.

### 9. Selecionar estrategia

Releia o catalogo a cada execucao. Escolher somente estrategia existente e compativel. Preferir uma unica estrategia e um unico terminal de execucao para evitar exposicao duplicada.

Respeitar bloqueios catalogados, inclusive lookback de `ScalperWinPullbackCurto`, parser booleano de `PriceActionSuporteResistencia`, reinicio do `OpeningRangeBreakout` e recalibracao de canais fixos.

### 10. Preparar candidatos

Partir de cada config ativo e alterar o minimo. Preservar campos desconhecidos, magic numbers e limites. Salvar os dois candidatos com `operar=false` por padrao.

Nao promover sem autorizacao explicita nesta execucao. Mesmo autorizado, promover no maximo um terminal, salvo autorizacao explicita para exposicao duplicada e limite agregado validado.

Exigir todos os gates:

1. conta, servidor, titular e modo confirmados por fonte independente/autoritativa;
2. em conta real, segunda confirmacao explicita identificando a conta e reconhecendo ordens reais;
3. nenhuma posicao ou conflito torna hot reload inseguro;
4. JSON, schema, parametros, estrategia e unidade validados;
5. sem evento iminente, rollover, stale data, divergencia material ou bloqueio de qualidade;
6. exposicao agregada entre `WINQ26`, `Bra50Aug26`, ETFs e acoes revisada.

Antes de promover, criar backup timestampado e substituir atomicamente. Se qualquer gate falhar, manter ativos intactos e documentar.

### 11. Finalizar e verificar

Usar `references/report-template.md`. O ultimo bloco deve ser `Proxima analise obrigatoria`, com horario absoluto e gatilhos antecipados.

Confirmar nomes, fontes/horarios, calculos, PesqEle, aliases separados, candidatos JSON validos/desabilitados, config ativo inalterado sem gates e proxima analise definida.
