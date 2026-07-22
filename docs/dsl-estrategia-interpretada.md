# Manual detalhado da DSL de estrategias interpretadas

**Estrategia do catalogo:** `EstrategiaInterpretada`
**Schema atual:** `1.0`
**Formato do arquivo:** JSON com extensao recomendada `.estrategia.json`
**Pasta padrao:** `config/`
**Objetivo:** criar e ajustar estrategias de entrada sem escrever ou recompilar codigo C#.

Este manual descreve a DSL interpretada do robo. A ideia central e simples: o motor C# registra uma unica estrategia chamada `EstrategiaInterpretada`; cada estrategia nova vira um arquivo JSON que diz quando comprar, quando vender, quais filtros globais aplicar, onde sugerir um stop estrutural e, opcionalmente, quando encerrar tecnicamente uma posicao aberta.

O arquivo JSON decide a entrada: `Comprar`, `Vender` ou `Aguardar`, com um motivo auditavel e opcionalmente um `StopSugerido`. Quando o bloco tecnico `saida` existe, ele tambem pode sinalizar fechamento de posicao aberta. Tudo que envolve dinheiro e execucao continua no motor compilado: lote, envio de ordem, fechamento a mercado, SL/TP de servidor, breakeven, trailing, parciais, limites diarios, cooldown, janelas globais e `RiskGuard`.

## 1. Quando usar a DSL

Use `EstrategiaInterpretada` quando voce quer:

- testar uma tese operacional rapidamente;
- criar variacoes de uma estrategia existente sem recompilar o Worker;
- escrever uma estrategia auditavel em JSON;
- separar claramente contexto, gatilho e stop;
- adicionar uma saida tecnica simples baseada em candle, indicador e dados da posicao aberta;
- rodar paridade contra estrategias compiladas.

Prefira uma estrategia C# nova quando:

- a regra exige estado complexo entre candles;
- a logica depende de fontes externas ainda nao expostas como operandos;
- a decisao exige estado complexo de gestao de posicao aberta;
- falta uma primitiva essencial na DSL.

## 2. Como ativar no arquivo de configuracao do simbolo

No arquivo principal do ativo, dentro de `config/`, registre a estrategia compilada `EstrategiaInterpretada` e aponte para o arquivo JSON da definicao.

```json
{
  "estrategias": [
    {
      "id": "dsl-reversao-suporte-1",
      "nome": "EstrategiaInterpretada",
      "magicNumber": 910,
      "ativa": true,
      "comprar": true,
      "vender": true,
      "saida": {
        "stopLossAtrMultiplo": 2.0,
        "takeProfitAtrMultiplo": 3.0
      },
      "gestaoDeRisco": {
        "modoLote": "loteFixo",
        "loteFixo": 1
      },
      "parametros": {
        "arquivoDefinicao": "reversao-suporte.estrategia.json"
      }
    }
  ]
}
```

Regras importantes:

- `nome` deve ser exatamente `EstrategiaInterpretada`, pois este e o nome registrado no catalogo do robo.
- `parametros.arquivoDefinicao` e relativo a pasta `config/`.
- O campo `nome` dentro do arquivo `.estrategia.json` e apenas um nome de exibicao/auditoria.
- Os flags `comprar` e `vender` do config principal continuam sendo respeitados. Se `comprar` estiver `false`, o bloco `compra` do JSON nao dispara ordem.

## 3. Ciclo de vida e hot-reload

Ao iniciar, o robo:

1. le `parametros.arquivoDefinicao`;
2. desserializa o JSON com nomes de propriedades case-insensitive;
3. rejeita campos desconhecidos;
4. valida schema, filtros, operadores, padroes, operandos e stops;
5. calcula automaticamente o lookback necessario;
6. passa a avaliar a definicao a cada ciclo.

O hot-reload observa o arquivo com `FileSystemWatcher`. Quando voce salva uma alteracao:

- se o JSON novo for valido, a definicao e trocada de forma atomica;
- se o JSON novo tiver erro, a versao anterior continua rodando e o erro e logado;
- se a primeira carga do arquivo for invalida, a estrategia retorna `Aguardar("Definicao invalida: ...")`.

O interpretador tambem protege historico insuficiente. Se a definicao pede indicadores ou candles que ainda nao existem, a decisao fica em `Aguardar`.

## 4. Estrutura minima do arquivo `.estrategia.json`

```json
{
  "$schemaVersion": "1.0",
  "nome": "MinhaPrimeiraEstrategia",
  "descricao": "Descricao curta da tese operacional.",
  "filtros": [],
  "compra": {
    "setup": { "todas": [] },
    "gatilho": { "todas": [] },
    "stop": {
      "tipo": "extremoCandle",
      "candle": 0,
      "lado": "minimo",
      "bufferPreco": 5
    }
  },
  "venda": {
    "setup": { "todas": [] },
    "gatilho": { "todas": [] },
    "stop": {
      "tipo": "extremoCandle",
      "candle": 0,
      "lado": "maximo",
      "bufferPreco": 5
    }
  },
  "saida": {
    "compra": { "todas": [] },
    "venda": { "todas": [] }
  }
}
```

- **Filtros**: opcionais, avaliados antes de tudo. Se um filtro falhar, a estrategia para imediatamente, poupando processamento.
- **Compra e Venda**: blocos independentes. Podem existir sozinhos (ex: robo so compra). Contem:
  - `setup`: condicao basica para estar no jogo (tendencia, volatilidade).
  - `gatilho`: condicao fina para entrar no candle atual (fechou rompendo topo).
  - `stop`: regra estrutural do stop inicial.
- **Saida**: bloco independente e opcional (avaliado via `IEstrategiaSaida`). Define condicoes de encerramento tecnico de posicoes abertas (compra ou venda).

Campos de topo:

| Campo | Obrigatorio | Descricao |
|---|---:|---|
| `$schemaVersion` | Sim | Deve ser `"1.0"`. |
| `nome` | Nao | Nome livre para auditoria. |
| `descricao` | Nao | Explicacao da tese. |
| `geradoEm` | Nao | Data/hora ou tag de geracao. |
| `geradoPor` | Nao | Origem da definicao, por exemplo Codex, comite ou analise. |
| `filtros` | Nao | Lista de filtros globais avaliados antes de compra/venda. |
| `compra` | Condicional | Bloco de compra. Pelo menos `compra` ou `venda` deve existir. |
| `venda` | Condicional | Bloco de venda. Pelo menos `compra` ou `venda` deve existir. |
| `saida` | Nao | Bloco tecnico para encerrar posicoes abertas da estrategia. |

Cada lado (`compra` ou `venda`) aceita:

| Campo | Obrigatorio | Descricao |
|---|---:|---|
| `setup` | Nao | Condicoes de contexto. Se omitido, passa como `setup omitido`. |
| `gatilho` | Nao | Confirmacao final. Se omitido, passa como `gatilho omitido`. |
| `stop` | Nao | Stop estrutural sugerido. Se omitido, o motor usa a configuracao generica de saida. |

## 5. Setup vs gatilho

Use `setup` para a leitura de contexto:

- tendencia;
- toque em suporte/resistencia;
- volatilidade aceitavel;
- padrao composto encontrado;
- pullback valido;
- alinhamento de medias.

Use `gatilho` para o disparo final:

- candle fechou acima da barra de sinal;
- EMA cruzou outra EMA;
- preco rompeu maxima/minima relevante;
- candle atual confirmou direcao.

Ambos precisam passar para a entrada acontecer. A separacao melhora os motivos de log, porque o robo consegue mostrar se a tese nem montou ou se estava montada mas faltou confirmacao.

## 6. Grupos logicos: `todas` e `qualquer`

Uma condicao pode ser:

- um grupo `todas`, equivalente a AND;
- um grupo `qualquer`, equivalente a OR;
- um operador (`op`);
- um padrao (`padrao`).

Exemplo com AND simples:

```json
{
  "todas": [
    { "op": ">", "a": "candle[0].fechamento", "b": "ema(21)[0]" },
    { "op": "entre", "a": "rsi(14)[0]", "min": 45, "max": 70 }
  ]
}
```

Exemplo com OR dentro do AND:

```json
{
  "todas": [
    { "op": ">", "a": "candle[0].fechamento", "b": "ema(21)[0]" },
    {
      "qualquer": [
        { "padrao": "trendBarAlta", "candle": 0, "corpoMinimoFracaoRange": 0.55 },
        { "padrao": "engolfoAlta", "candle": 0 }
      ]
    }
  ]
}
```

Limites de validacao:

- a condicao nao pode misturar `todas`, `qualquer`, `op` e `padrao` no mesmo objeto;
- grupos vazios sao permitidos pelo avaliador, mas nao sao uteis;
- o aninhamento logico e limitado a 2 niveis uteis dentro do bloco.

## 7. Convencao de candles

`candle[0]` e sempre o candle fechado mais recente. `candle[1]` e o candle fechado anterior, `candle[2]` o anterior a ele, e assim por diante.

Exemplos:

```text
candle[0].fechamento  -> fechamento do ultimo candle fechado
candle[1].maximo      -> maxima do candle anterior
candle[5].minimo      -> minima de cinco candles atras
```

Indices negativos sao rejeitados.

## 8. Operandos: fontes de dados

Operandos sao valores numericos que podem ser lidos pelo interpretador. Eles aparecem nos campos `a`, `b`, `min`, `max`, em `serie` e em alguns stops.

### 8.1 Literais numericos

```json
{ "op": ">=", "a": "rsi(14)[0]", "b": 50 }
```

Numeros podem ser escritos como numero JSON ou string. Internamente, o validador converte para string e usa cultura invariante.

### 8.2 Dados de candle

| Operando | Significado |
|---|---|
| `candle[n].abertura` | Abertura do candle. |
| `candle[n].maximo` | Maxima do candle. |
| `candle[n].minimo` | Minima do candle. |
| `candle[n].fechamento` | Fechamento do candle. |
| `candle[n].volume` | Volume do candle. |
| `candle[n].range` | Maxima menos minima. |
| `candle[n].corpo` | Fechamento menos abertura, com sinal. |
| `candle[n].corpoAbs` | Tamanho absoluto do corpo. |
| `candle[n].pavioSuperior` | Distancia entre topo do corpo e maxima. |
| `candle[n].pavioInferior` | Distancia entre fundo do corpo e minima. |
| `candle[n].posFechamentoRange` | Posicao do fechamento no range: 0 = minima, 1 = maxima. |

### 8.3 Tick atual

| Operando | Significado |
|---|---|
| `tick.bid` | Bid atual. |
| `tick.ask` | Ask atual. |
| `tick.spread` | `abs(ask - bid)`. |

*(Nota: O operando `tick.*` é **proibido** no bloco `saida`, pois a saída técnica é avaliada estritamente no fechamento de cada candle e não suporta variação intra-candle para sinais baseados em tick).*

### 8.4 Dados de Posição (Exclusivos do bloco `saida`)

| Operando | Significado |
|---|---|
| `posicao.lucroBruto` | Lucro acumulado da posição atual em R$ (pode ser negativo). |
| `posicao.precoEntrada` | O preço onde a posição foi aberta no MT5. |
| `posicao.lucroPercentualPreco` | A variação percentual atual do preço em relação ao preço de entrada. Positivo se a favor da posição. |
*(Nota: Estes operandos são **restritos ao bloco `saida`**. Tentar usá-los nos blocos de `compra` ou `venda` causará falha na validação do arquivo).*

### 8.5 Indicadores

| Operando | Significado |
|---|---|
| `ema(periodo)[n]` | EMA no timeframe base. |
| `sma(periodo)[n]` | SMA no timeframe base. |
| `smma(periodo)[n]` | SMMA no timeframe base. |
| `rsi(periodo)[n]` | RSI. |
| `atr(periodo)[n]` | ATR. |
| `vwap[n]` | VWAP. O periodo e ignorado. |
| `mediaPrecoMediano("SMMA", periodo)[n]` | Media sobre preco mediano `(high+low)/2`, usando SMMA. |
| `mediaPrecoMediano("SMA", periodo)[n]` | Media sobre preco mediano, usando SMA. |

Exemplos:

```text
ema(9)[0]
ema(21)[1]
rsi(14)[0]
atr(14)[0]
vwap[0]
mediaPrecoMediano("SMMA", 5)[0]
```

### 8.5 Multi-timeframe

O interpretador aceita timeframe como segundo argumento em medias, indicadores e candles agregados. Os candles M1 sao agregados para timeframes maiores.

Formatos aceitos:

```text
M1, M5, M15, M30, H1, H4, D1, W1, MN
```

Exemplos:

```text
ema(21, "M5")[0]
smma(5, "M15")[0]
candle("M5")[0].fechamento
```

Observacao pratica: o uso mais comum e `candle[n].campo` no timeframe base. Para confirmacao em timeframe maior, prefira medias e indicadores, como `ema(21, "M5")[0]`, porque esse e o caminho mais testado no projeto.

### 8.6 Niveis manuais de suporte e resistencia

| Operando | Significado |
|---|---|
| `nivel.suporte.preco` | Suporte configurado mais proximo do `tick.bid`. |
| `nivel.resistencia.preco` | Resistencia configurada mais proxima do `tick.bid`. |

Os niveis vem de `parametros.niveis` no config principal da estrategia/ativo. Se nao houver nivel do tipo esperado, o operando retorna nulo e a condicao falha.

### 8.7 Extremos e janelas

| Operando | Significado |
|---|---|
| `maxima(de, ate)` | Maior maxima entre os candles de indice `de` ate `ate`. |
| `minima(de, ate)` | Menor minima entre os candles de indice `de` ate `ate`. |
| `maximaJanela("09:00","09:15")` | Maior maxima dos candles do dia dentro da janela. |
| `minimaJanela("09:00","09:15")` | Menor minima dos candles do dia dentro da janela. |
| `rangeMedio(20)` | Media do range dos ultimos 20 candles. |
| `fibRet(de, ate, nivel, "alta")` | Retracao Fibonacci automatica de alta: `maxima - (range * nivel)`. |
| `fibRet(de, ate, nivel, "baixa")` | Retracao Fibonacci automatica de baixa: `minima + (range * nivel)`. |

Exemplos:

```json
{ "op": ">", "a": "tick.ask", "b": "maximaJanela(\"09:00\",\"09:15\")" }
```

```json
{ "op": "<", "a": "candle[0].minimo", "b": "minima(1, 5)" }
```

```json
{ "op": "<", "a": "candle[0].fechamento", "b": "fibRet(0, 80, 0.786, \"baixa\")" }
```

Em tendencia de baixa, `fibRet(0, 80, 0.618, "baixa")` calcula a retracao de 61.8%
acima da minima do swing. Em tendencia de alta, `fibRet(0, 80, 0.618, "alta")`
calcula a retracao de 61.8% abaixo da maxima do swing.

### 8.8 Referencias de padroes compostos

Padroes compostos podem salvar um candle nomeado em `idRef`. Depois, voce pode usar:

```text
ref.nome.maximo
ref.nome.minimo
ref.nome.abertura
ref.nome.fechamento
ref.nome.volume
ref.nome.range
ref.nome.corpo
ref.nome.corpoAbs
ref.nome.pavioSuperior
ref.nome.pavioInferior
```

Exemplo:

```json
{
  "padrao": "impulsoPullback",
  "idRef": "barraSinal",
  "lado": "alta"
}
```

Depois:

```json
{ "op": ">", "a": "candle[0].fechamento", "b": "ref.barraSinal.maximo" }
```

## 9. Aritmetica em operandos

O parser aceita uma modificacao simples no final do operando:

```text
ema(156)[0] + 170
ema(156)[0] - 170
candle[0].corpoAbs * 2
```

Limites:

- apenas um operador por operando;
- operadores aceitos: `+`, `-`, `*`;
- o valor da direita deve ser literal numerico positivo;
- nao ha parenteses, divisao, funcoes arbitrarias nem expressoes livres.

Exemplos validos:

```json
{ "op": ">", "a": "tick.ask", "b": "ema(156)[0] + 170" }
```

```json
{ "op": "<=", "a": "candle[0].range", "b": "atr(14)[0] * 1.5" }
```

## 10. Operadores de condicao

### 10.1 Comparacoes

```json
{ "op": ">", "a": "candle[0].fechamento", "b": "ema(21)[0]" }
```

Operadores aceitos:

| Operador | Semantica |
|---|---|
| `>` | `a > b` |
| `>=` | `a >= b` |
| `<` | `a < b` |
| `<=` | `a <= b` |
| `==` | igualdade com tolerancia interna pequena |

### 10.2 `entre`

```json
{ "op": "entre", "a": "rsi(14)[0]", "min": 40, "max": 70 }
```

Passa quando `a >= min && a <= max`.

### 10.3 Cruzamentos

```json
{ "op": "cruzouAcima", "a": "ema(9)", "b": "ema(21)" }
```

`cruzouAcima` passa quando:

```text
a[1] <= b[1] && a[0] > b[0]
```

`cruzouAbaixo` passa quando:

```text
a[1] >= b[1] && a[0] < b[0]
```

Voce pode escrever `ema(9)` sem indice; o indice padrao e 0. O avaliador busca automaticamente o candle anterior para detectar o cruzamento.

### 10.4 Proximidade e distancia

```json
{
  "op": "pertoDe",
  "a": "tick.bid",
  "b": "nivel.suporte.preco",
  "toleranciaPreco": 35
}
```

| Operador | Campos extras | Semantica |
|---|---|---|
| `pertoDe` | `toleranciaPreco` | `abs(a - b) <= toleranciaPreco` |
| `distanciaMinima` | `valorPreco` | `abs(a - b) >= valorPreco` |
| `distanciaMaxima` | `valorPreco` | `abs(a - b) <= valorPreco` |

### 10.5 `alinhados`

```json
{
  "op": "alinhados",
  "serie": ["ema(9)[0]", "ema(21)[0]", "ema(50)[0]"],
  "ordem": "decrescente"
}
```

Regras:

- `serie` deve ter pelo menos 2 operandos;
- `ordem` deve ser `crescente` ou `decrescente`;
- `crescente`: primeiro valor menor que o segundo, segundo menor que o terceiro;
- `decrescente`: primeiro valor maior que o segundo, segundo maior que o terceiro.

Uso tipico:

- `decrescente` para medias empilhadas em tendencia de alta, quando a media curta esta acima da media longa;
- `crescente` para tendencia de baixa.

### 10.6 `inclinacao`

```json
{
  "op": "inclinacao",
  "a": "ema(21)",
  "candles": 3,
  "direcao": "alta"
}
```

Passa quando o valor atual de `a` e comparado com o mesmo operando alguns candles atras:

- `direcao: "alta"` passa se `a[0] > a[candles]`;
- `direcao: "baixa"` passa se `a[0] < a[candles]`.

## 11. Padroes de candle

Padroes sao condicoes prontas usando o campo `padrao` em vez de `op`.

### 11.1 `trendBarAlta` e `trendBarBaixa`

```json
{
  "padrao": "trendBarAlta",
  "candle": 0,
  "corpoMinimoFracaoRange": 0.55
}
```

Passa quando:

- `trendBarAlta`: corpo positivo e corpo/range >= fracao;
- `trendBarBaixa`: corpo negativo e abs(corpo)/range >= fracao.

Se `corpoMinimoFracaoRange` for omitido, o padrao usa `0.55`.

### 11.2 `rejeicaoCompradora` e `rejeicaoVendedora`

```json
{
  "padrao": "rejeicaoCompradora",
  "candle": 0,
  "multiploPavio": 2.0
}
```

`rejeicaoCompradora` passa quando:

- pavio inferior >= corpo absoluto x `multiploPavio`;
- candle fechou acima da abertura.

`rejeicaoVendedora` passa quando:

- pavio superior >= corpo absoluto x `multiploPavio`;
- candle fechou abaixo da abertura.

Se `multiploPavio` for omitido, o padrao usa `2.0`.

### 11.3 `fechamentoDirecional`

```json
{
  "padrao": "fechamentoDirecional",
  "candle": 0,
  "lado": "alta"
}
```

`lado` aceita:

- `alta`: fechamento acima da abertura;
- `baixa`: fechamento abaixo da abertura.

### 11.4 `sequencia`

```json
{
  "padrao": "sequencia",
  "lado": "alta",
  "candles": 3
}
```

Passa se os ultimos `candles` candles tiverem corpo na direcao indicada.

### 11.5 `insideBar` e `outsideBar`

```json
{ "padrao": "insideBar", "candle": 0 }
```

- `insideBar`: maxima do candle <= maxima anterior e minima do candle >= minima anterior.
- `outsideBar`: maxima do candle > maxima anterior e minima do candle < minima anterior.

### 11.6 `engolfoAlta` e `engolfoBaixa`

```json
{ "padrao": "engolfoAlta", "candle": 0 }
```

- `engolfoAlta`: candle anterior baixista, candle atual altista, corpo atual engolfa o corpo anterior.
- `engolfoBaixa`: candle anterior altista, candle atual baixista, corpo atual engolfa o corpo anterior.

## 12. Padroes compostos

Padroes compostos podem olhar uma janela de candles e registrar uma referencia (`idRef`) para uso posterior.

### 12.1 `impulsoPullback`

Procura um impulso direcional, depois um pullback controlado, e salva a ultima barra do pullback como referencia quando `idRef` e informado.

Exemplo:

```json
{
  "padrao": "impulsoPullback",
  "idRef": "barraSinal",
  "lado": "alta",
  "corpoMinimoFracaoRange": 0.55,
  "minCandlesImpulso": 2,
  "maxCandlesImpulso": 6,
  "minCandlesPullback": 1,
  "maxCandlesPullback": 5,
  "maxPullbackPercentual": 0.7,
  "inicioCandleSinal": 1,
  "fimCandleSinal": 3,
  "lookbackMedioRange": 20,
  "climaxMultiploRange": 2.5
}
```

Campos:

| Campo | Padrao | Descricao |
|---|---:|---|
| `lado` | Obrigatorio | `alta` ou `baixa`. |
| `idRef` | Opcional | Nome da referencia salva. |
| `corpoMinimoFracaoRange` | `0.55` | Fracao minima do corpo para classificar trend bars. |
| `minCandlesImpulso` | `2` | Minimo de candles do impulso. |
| `maxCandlesImpulso` | `6` | Maximo de candles do impulso. |
| `minCandlesPullback` | `1` | Minimo de candles do pullback. |
| `maxCandlesPullback` | `5` | Maximo de candles do pullback. |
| `maxPullbackPercentual` | `0.7` | Pullback maximo em relacao a amplitude do impulso. |
| `inicioCandleSinal` | `1` | Primeiro offset aceito para barra de sinal. |
| `fimCandleSinal` | Igual ao inicio | Ultimo offset aceito para barra de sinal. |
| `climaxMultiploRange` | Opcional | Bloqueia impulso com candle maior que media de range x multiplo. |
| `lookbackMedioRange` | `20` | Lookback usado no filtro de climax. |

Uso tipico com gatilho:

```json
"gatilho": {
  "todas": [
    { "op": ">", "a": "candle[0].fechamento", "b": "ref.barraSinal.maximo" }
  ]
}
```

### 12.2 `rompimentoReteste`

Procura rompimento de suporte/resistencia e verifica se o rompimento nao foi devolvido.

```json
{
  "padrao": "rompimentoReteste",
  "idRef": "barraRompimento",
  "lado": "alta",
  "tipoNivel": "resistencia",
  "toleranciaPreco": 10,
  "fimCandleSinal": 5
}
```

Campos:

| Campo | Obrigatorio | Descricao |
|---|---:|---|
| `lado` | Sim | `alta` ou `baixa`. |
| `tipoNivel` | Sim | `suporte` ou `resistencia`. |
| `toleranciaPreco` | Nao | Tolerancia em pontos/preco. Padrao `0`. |
| `fimCandleSinal` | Nao | Quantos candles para tras procurar rompimento. Padrao `5`. |
| `idRef` | Nao | Salva a barra de rompimento. |

### 12.3 `tocouNivel`

Verifica se algum candle recente tocou o suporte/resistencia.

```json
{
  "padrao": "tocouNivel",
  "idRef": "toque",
  "tipoNivel": "suporte",
  "lado": "minimo",
  "toleranciaPreco": 35,
  "fimCandleSinal": 3
}
```

Campos:

| Campo | Obrigatorio | Descricao |
|---|---:|---|
| `tipoNivel` | Sim | `suporte` ou `resistencia`. |
| `lado` | Nao | `minimo`, `maximo` ou omitido para range completo. |
| `toleranciaPreco` | Nao | Distancia maxima do nivel. Padrao `0`. |
| `fimCandleSinal` | Nao | Quantos candles recentes procurar. Padrao `3`. |
| `idRef` | Nao | Salva o candle que tocou. |

## 13. Filtros globais

Filtros sao avaliados antes de compra e venda. Se um filtro falha, a estrategia retorna `Aguardar` imediatamente.

### 13.1 `spreadMaximo`

```json
{ "tipo": "spreadMaximo", "valorPreco": 15 }
```

Passa quando `tick.spread <= valorPreco`.

### 13.2 `atrMinimo` e `atrMaximo`

```json
{ "tipo": "atrMinimo", "periodo": 14, "valorPreco": 30 }
```

```json
{ "tipo": "atrMaximo", "periodo": 14, "valorPreco": 250 }
```

Passa quando o ATR esta dentro do limite desejado.

### 13.3 `candleMaxAtrMultiplo`

```json
{ "tipo": "candleMaxAtrMultiplo", "periodo": 14, "multiplo": 2.0 }
```

Passa quando o range do candle atual e menor ou igual a `ATR(periodo) * multiplo`.

### 13.4 `janelaHorario`

```json
{ "tipo": "janelaHorario", "inicio": "09:05", "fim": "17:30" }
```

Passa quando o horario do tick esta dentro da janela. Janelas que viram a meia-noite tambem sao aceitas.

### 13.5 `semClimax`

```json
{
  "tipo": "semClimax",
  "janelaCandles": 5,
  "multiploRange": 2.5,
  "lookbackMedioRange": 20
}
```

Calcula `rangeMedio(lookbackMedioRange)` e reprova se qualquer candle recente da janela tiver range maior que `media * multiploRange`.

## 14. Stop estrutural

O campo `stop` e opcional. Quando existe, ele sugere um preco de stop para a decisao. A regra de buffer depende do lado:

- em compra: `valorBase - bufferPreco`;
- em venda: `valorBase + bufferPreco`.

`bufferPreco` e obrigatorio em todos os tipos de stop.

### 14.1 `extremoCandle`

```json
{
  "tipo": "extremoCandle",
  "candle": 0,
  "lado": "minimo",
  "bufferPreco": 5
}
```

Usa a minima ou maxima de um candle.

Campos:

- `candle`: indice do candle;
- `lado`: `minimo` ou `maximo`;
- `bufferPreco`: distancia adicionada/subtraida.

### 14.2 `valorOperando`

```json
{
  "tipo": "valorOperando",
  "a": "ema(21)[0]",
  "bufferPreco": 15
}
```

Usa qualquer operando numerico como base do stop.

### 14.3 `extremoRef`

```json
{
  "tipo": "extremoRef",
  "ref": "barraSinal",
  "lado": "minimo",
  "bufferPreco": 5
}
```

Usa minima ou maxima de uma referencia criada por padrao composto. A referencia precisa ter sido declarada por `idRef` no mesmo bloco de lado.

### 14.4 `nivelProximo`

```json
{
  "tipo": "nivelProximo",
  "tipoNivel": "suporte",
  "bufferPreco": 50
}
```

Usa o suporte ou resistencia configurado mais proximo do preco.

## 15. Saida tecnica de posicao aberta

Alem de decidir a entrada, uma definicao pode encerrar posicoes abertas por leitura tecnica. O bloco opcional `saida`, no topo do arquivo `.estrategia.json` ao lado de `compra` e `venda`, descreve as condicoes de fechamento.

Quem executa o fechamento e sempre o motor compilado, pelo `GerenciadorPosicoesAbertasService`, a mercado, com o motivo registrado no log. A DSL apenas responde se a posicao deve ser fechada.

### 15.1 Estrutura

```json
"saida": {
  "compra": {
    "todas": [
      { "op": "cruzouAbaixo", "a": "ema(9)", "b": "ema(21)" }
    ]
  },
  "venda": {
    "qualquer": [
      { "op": "cruzouAcima", "a": "ema(9)", "b": "ema(21)" },
      { "padrao": "rejeicaoCompradora", "candle": 0, "multiploPavio": 2.0 }
    ]
  }
}
```

| Campo | Obrigatorio | Descricao |
|---|---:|---|
| `saida.compra` | Condicional | Condicoes para encerrar uma posicao comprada. |
| `saida.venda` | Condicional | Condicoes para encerrar uma posicao vendida. |

Pelo menos um dos dois lados deve existir se o bloco `saida` for declarado. Bloco `saida` vazio e rejeitado na validacao.

Atencao a semantica dos nomes: `saida.compra` nao significa "sair comprando"; significa "condicoes de saida da posicao comprada". Uma posicao comprada avalia somente `saida.compra`; uma posicao vendida avalia somente `saida.venda`.

### 15.2 Quando o bloco e avaliado

- somente quando existe posicao aberta da estrategia, pelo mesmo `magicNumber`;
- somente uma vez por candle fechado novo, nunca por tick;
- o robo guarda em memoria o tempo do ultimo candle avaliado por ticket;
- salvar o arquivo nao reavalia o mesmo candle ja marcado em memoria;
- ao reiniciar o Worker, esse controle em memoria e reiniciado; se ainda houver posicao aberta, o candle fechado atual pode ser avaliado novamente;
- se a condicao do lado correspondente for verdadeira, a posicao e fechada a mercado com rastro no log.

Exemplo de log:

```text
[GestaoDinamica] WINQ26/EstrategiaInterpretada Ticket=123456: saida/compra: cruzouAbaixo OK (...)
```

### 15.3 Vocabulario permitido

Dentro de `saida` valem os mesmos grupos (`todas` e `qualquer`), operadores, padroes de candle e indicadores dos blocos de entrada, com estas diferencas:

1. Operandos de posicao exclusivos do bloco `saida`:
   - `posicao.precoEntrada`;
   - `posicao.lucroBruto`;
   - `posicao.lucroPercentualPreco`.
2. `tick.*` e proibido no bloco `saida`. A avaliacao ocorre em candle fechado; use `candle[0].fechamento` no lugar de `tick.bid` ou `tick.ask`.
3. `ref.*` e padroes que exportam referencias via `idRef` sao proibidos. A saida e avaliada de forma independente da entrada; referencias criadas na entrada nao existem aqui.

O validador rejeita esses usos na carga ou no hot-reload. Se a primeira carga for invalida, a estrategia fica aguardando com erro de validacao; se for hot-reload invalido, a versao anterior continua rodando.

Exemplo usando operando de posicao:

```json
"saida": {
  "compra": {
    "todas": [
      { "op": ">=", "a": "posicao.lucroPercentualPreco", "b": 0.3 },
      { "padrao": "rejeicaoVendedora", "candle": 0, "multiploPavio": 2.0 }
    ]
  }
}
```

Esse exemplo fecha uma compra somente quando a posicao ja tem pelo menos `0.3%` de variacao favoravel no preco e aparece uma rejeicao vendedora no candle fechado.

### 15.4 Interacao com as demais saidas

A saida tecnica e uma camada adicional, nunca substituta:

- o SL/TP de servidor continua obrigatorio e protege mesmo com o robo desligado;
- a saida tecnica e executada pelo gerenciador de posicoes abertas, nao pelo avaliador de entrada;
- a primeira condicao que disparar fecha a posicao; as demais nao sao avaliadas naquele ciclo;
- breakeven, trailing e parciais continuam sendo gerenciados pelo bloco `saida` do config principal da estrategia/ativo (`SaidaConfig`), nao pelo bloco tecnico do `.estrategia.json`.

No ciclo de monitoramento do `GerenciadorPosicoesAbertasService`, a precedencia e:

1. saida por valor bruto;
2. saida por percentual da conta;
3. saida tecnica da DSL;
4. encerramento por preco (`encerrarAtivaAcimaDe` / `encerrarAtivaAbaixoDe`);
5. parciais, breakeven e trailing, que ajustam ou reduzem posicao remanescente.

### 15.5 Erros comuns

- usar `tick.bid`, `tick.ask` ou `tick.spread` dentro de `saida`: troque por `candle[0].fechamento`;
- inverter os lados: `saida.compra` e para sair de posicao comprada; `saida.venda` e para sair de posicao vendida;
- criar uma condicao que nunca dispara, deixando a posicao depender apenas do SL/TP de servidor;
- esperar memoria entre candles, como "lucro maximo atingido"; a avaliacao e pura sobre janela de candles e posicao atual;
- tentar usar `ref.barraSinal.*` ou `idRef` em saida tecnica.

Para trailing de verdade, use os campos `trailingStop*` do config do simbolo/estrategia.

## 16. Exemplos completos

### 16.1 Reversao compradora/vendedora em suporte/resistencia

```json
{
  "$schemaVersion": "1.0",
  "nome": "ReversaoRejeicaoSuporteResistencia",
  "descricao": "Compra rejeicao em suporte com RSI baixo; vende rejeicao em resistencia com RSI alto.",
  "filtros": [
    { "tipo": "spreadMaximo", "valorPreco": 15 },
    { "tipo": "atrMinimo", "periodo": 14, "valorPreco": 30 },
    { "tipo": "janelaHorario", "inicio": "09:05", "fim": "17:30" }
  ],
  "compra": {
    "setup": {
      "todas": [
        { "padrao": "tocouNivel", "tipoNivel": "suporte", "lado": "minimo", "toleranciaPreco": 35 },
        { "padrao": "rejeicaoCompradora", "candle": 0, "multiploPavio": 2.0 },
        { "op": "<=", "a": "rsi(14)[0]", "b": 40 }
      ]
    },
    "gatilho": {
      "todas": [
        { "op": ">", "a": "candle[0].fechamento", "b": "ema(9)[0]" }
      ]
    },
    "stop": {
      "tipo": "nivelProximo",
      "tipoNivel": "suporte",
      "bufferPreco": 50
    }
  },
  "venda": {
    "setup": {
      "todas": [
        { "padrao": "tocouNivel", "tipoNivel": "resistencia", "lado": "maximo", "toleranciaPreco": 35 },
        { "padrao": "rejeicaoVendedora", "candle": 0, "multiploPavio": 2.0 },
        { "op": ">=", "a": "rsi(14)[0]", "b": 60 }
      ]
    },
    "gatilho": {
      "todas": [
        { "op": "<", "a": "candle[0].fechamento", "b": "ema(9)[0]" }
      ]
    },
    "stop": {
      "tipo": "nivelProximo",
      "tipoNivel": "resistencia",
      "bufferPreco": 50
    }
  }
}
```

### 16.2 Cruzamento de EMAs com filtro de RSI

```json
{
  "$schemaVersion": "1.0",
  "nome": "CruzamentoEmaDsl",
  "filtros": [
    { "tipo": "spreadMaximo", "valorPreco": 15 }
  ],
  "compra": {
    "setup": {
      "todas": [
        { "op": "entre", "a": "rsi(14)[0]", "min": 40, "max": 70 }
      ]
    },
    "gatilho": {
      "todas": [
        { "op": "cruzouAcima", "a": "ema(9)", "b": "ema(21)" }
      ]
    }
  },
  "venda": {
    "setup": {
      "todas": [
        { "op": "entre", "a": "rsi(14)[0]", "min": 30, "max": 60 }
      ]
    },
    "gatilho": {
      "todas": [
        { "op": "cruzouAbaixo", "a": "ema(9)", "b": "ema(21)" }
      ]
    }
  }
}
```

### 16.3 Price Action Bar-by-Bar com referencia de barra de sinal

Este exemplo e semelhante ao arquivo real `config/barbybar-paridade.estrategia.json`.

```json
{
  "$schemaVersion": "1.0",
  "nome": "PriceActionBarByBarParidade",
  "compra": {
    "setup": {
      "todas": [
        {
          "padrao": "impulsoPullback",
          "idRef": "barraSinal",
          "lado": "alta",
          "corpoMinimoFracaoRange": 0.55,
          "minCandlesImpulso": 2,
          "maxCandlesImpulso": 6,
          "minCandlesPullback": 1,
          "maxCandlesPullback": 5,
          "maxPullbackPercentual": 0.7,
          "lookbackMedioRange": 20,
          "climaxMultiploRange": 2.5
        }
      ]
    },
    "gatilho": {
      "todas": [
        { "op": ">", "a": "candle[0].fechamento", "b": "ref.barraSinal.maximo" }
      ]
    },
    "stop": {
      "tipo": "extremoRef",
      "ref": "barraSinal",
      "lado": "minimo",
      "bufferPreco": 5
    }
  },
  "venda": {
    "setup": {
      "todas": [
        {
          "padrao": "impulsoPullback",
          "idRef": "barraSinal",
          "lado": "baixa",
          "corpoMinimoFracaoRange": 0.55,
          "minCandlesImpulso": 2,
          "maxCandlesImpulso": 6,
          "minCandlesPullback": 1,
          "maxCandlesPullback": 5,
          "maxPullbackPercentual": 0.7,
          "lookbackMedioRange": 20,
          "climaxMultiploRange": 2.5
        }
      ]
    },
    "gatilho": {
      "todas": [
        { "op": "<", "a": "candle[0].fechamento", "b": "ref.barraSinal.minimo" }
      ]
    },
    "stop": {
      "tipo": "extremoRef",
      "ref": "barraSinal",
      "lado": "maximo",
      "bufferPreco": 5
    }
  }
}
```

### 16.4 Opening Range Breakout

```json
{
  "$schemaVersion": "1.0",
  "nome": "OpeningRangeBreakoutDsl",
  "filtros": [
    { "tipo": "janelaHorario", "inicio": "09:15", "fim": "12:00" },
    { "tipo": "spreadMaximo", "valorPreco": 15 }
  ],
  "compra": {
    "gatilho": {
      "todas": [
        { "op": ">", "a": "tick.ask", "b": "maximaJanela(\"09:00\",\"09:15\")" },
        { "padrao": "trendBarAlta", "candle": 0, "corpoMinimoFracaoRange": 0.50 }
      ]
    },
    "stop": {
      "tipo": "valorOperando",
      "a": "minimaJanela(\"09:00\",\"09:15\")",
      "bufferPreco": 5
    }
  },
  "venda": {
    "gatilho": {
      "todas": [
        { "op": "<", "a": "tick.bid", "b": "minimaJanela(\"09:00\",\"09:15\")" },
        { "padrao": "trendBarBaixa", "candle": 0, "corpoMinimoFracaoRange": 0.50 }
      ]
    },
    "stop": {
      "tipo": "valorOperando",
      "a": "maximaJanela(\"09:00\",\"09:15\")",
      "bufferPreco": 5
    }
  }
}
```

## 17. Como validar mentalmente antes de rodar

Antes de salvar uma estrategia nova, confira:

- O arquivo tem `"$schemaVersion": "1.0"`.
- Existe pelo menos um bloco `compra` ou `venda`.
- Cada condicao tem apenas um destes campos: `todas`, `qualquer`, `op` ou `padrao`.
- Operadores usam os campos obrigatorios corretos.
- Padroes simples com candle usam `candle >= 0`.
- Stops sempre tem `bufferPreco`.
- `extremoRef` usa uma `ref` criada por `idRef` no mesmo lado.
- Niveis de suporte/resistencia existem no config principal quando usados.
- Timeframes estao no formato aceito, como `"M5"` ou `"H1"`.
- O JSON nao tem comentarios, virgulas sobrando ou campos inventados.

## 18. Erros comuns

### Campo desconhecido

O carregador rejeita membros nao mapeados. Exemplo ruim:

```json
{ "op": ">", "a": "rsi(14)[0]", "valor": 50 }
```

Use:

```json
{ "op": ">", "a": "rsi(14)[0]", "b": 50 }
```

### Usar `direcao` em padrao que espera `lado`

O codigo atual usa `lado` para padroes como `impulsoPullback`, `rompimentoReteste`, `sequencia` e `fechamentoDirecional`.

Exemplo correto:

```json
{ "padrao": "impulsoPullback", "lado": "alta", "idRef": "barraSinal" }
```

### Esquecer `bufferPreco` no stop

Todo stop precisa de `bufferPreco`, inclusive `valorOperando`.

### Usar referencia nao declarada

Exemplo ruim:

```json
"stop": { "tipo": "extremoRef", "ref": "barraSinal", "lado": "minimo", "bufferPreco": 5 }
```

Sem antes declarar:

```json
{ "padrao": "impulsoPullback", "idRef": "barraSinal", "lado": "alta" }
```

### Confundir ordem de `alinhados`

Se a serie e `["ema(9)", "ema(21)", "ema(50)"]`:

- tendencia de alta normalmente usa `decrescente`, pois EMA 9 > EMA 21 > EMA 50;
- tendencia de baixa normalmente usa `crescente`, pois EMA 9 < EMA 21 < EMA 50.

## 19. Referencia rapida

### Filtros

```text
spreadMaximo(valorPreco)
atrMinimo(periodo, valorPreco)
atrMaximo(periodo, valorPreco)
candleMaxAtrMultiplo(periodo, multiplo)
janelaHorario(inicio, fim)
semClimax(janelaCandles, multiploRange, lookbackMedioRange)
```

### Operadores

```text
>
>=
<
<=
==
entre(a, min, max)
cruzouAcima(a, b)
cruzouAbaixo(a, b)
pertoDe(a, b, toleranciaPreco)
distanciaMinima(a, b, valorPreco)
distanciaMaxima(a, b, valorPreco)
alinhados(serie, ordem)
inclinacao(a, candles, direcao)
```

### Padroes

```text
trendBarAlta
trendBarBaixa
rejeicaoCompradora
rejeicaoVendedora
fechamentoDirecional
sequencia
insideBar
outsideBar
engolfoAlta
engolfoBaixa
impulsoPullback
rompimentoReteste
tocouNivel
```

### Stops

```text
extremoCandle(candle, lado, bufferPreco)
valorOperando(a, bufferPreco)
extremoRef(ref, lado, bufferPreco)
nivelProximo(tipoNivel, bufferPreco)
```

## 20. Arquivos de codigo relacionados

- `src/Financial.Robot.Worker/Strategy/Interpretada/EstrategiaInterpretada.cs`: ciclo principal de avaliacao, hot-reload, filtros e lookback.
- `src/Financial.Robot.Worker/Strategy/Interpretada/DefinicaoEstrategia.cs`: modelo JSON aceito.
- `src/Financial.Robot.Worker/Strategy/Interpretada/ValidadorDefinicao.cs`: regras de validacao.
- `src/Financial.Robot.Worker/Strategy/Interpretada/ParserOperando.cs`: gramatica dos operandos.
- `src/Financial.Robot.Worker/Strategy/Interpretada/AvaliadorCondicoes.cs`: operadores logicos e comparacoes.
- `src/Financial.Robot.Worker/Strategy/Interpretada/PadroesCandle.cs`: padroes simples.
- `src/Financial.Robot.Worker/Strategy/Interpretada/PadroesCompostos.cs`: padroes com janela e referencias.
- `src/Financial.Robot.Worker/Strategy/Interpretada/CalculadoraStop.cs`: stops estruturais.
- `src/Financial.Robot.Worker/Strategy/Interpretada/ContextoAvaliacao.cs`: resolucao de candles, indicadores, niveis e refs.

## 21. Regra de ouro

Se a estrategia ficou dificil demais de ler no JSON, nao force. Crie uma primitiva nova em C#, teste essa primitiva, e mantenha a DSL declarativa e auditavel. O arquivo `.estrategia.json` deve explicar a tese de trading sem virar um programa escondido.
