# Modo Strategy Tester (MtApi5)

O modo `--mode mtapi-tester` foi criado para validar de forma autônoma a integração entre a biblioteca `MtApi5Client` em C# e o Expert Advisor `MtApi5.ex5` executando dentro do **Strategy Tester** do MetaTrader 5.

O Strategy Tester do MT5 roda em um ambiente completamente isolado do ambiente "Live" de trading. Isso significa que ele não pode usar o mesmo canal de comunicação (porta TCP 8228) que o terminal principal usa, sob risco de corromper o estado do robô.

## Objetivo

Garantir que as chamadas fundamentais da API (como consulta de conta, preços e histórico) funcionem perfeitamente em backtest antes de executarmos simulações pesadas com o `BacktestCliRunner`.

## Passo a Passo

### 1. Configurando o MetaTrader 5

1. Abra o MetaTrader 5.
2. Pressione `Ctrl + R` para abrir a janela do **Strategy Tester** (Testador de Estratégias).
3. Selecione o Expert Advisor `MtApi5.ex5` (localizado em sua pasta de Experts/MtApi).
4. Escolha o ativo (ex: `WINQ26`) e o período/timeframe (ex: `M1`).
5. Na aba **Inputs** (Parâmetros de Entrada) do Expert Advisor, modifique o parâmetro **Port** para `8230`. *(Isso garante que o teste rode numa porta separada do robô em produção)*
6. Clique em **Start** (Iniciar) no Strategy Tester.

Neste momento, você deverá ver na aba **Journal** (Diário) do Strategy Tester uma mensagem como:
> *"MtApi5: Waiting on remote client..."*

Isso significa que o EA entrou no estado `IsTesting()` e pausou o teste até que o cliente C# conecte e o libere via comando `BacktestingReady`.

### 2. Executando o Harness (.NET)

Abra o PowerShell na pasta raiz do projeto (`D:\SistemEarobot\financial.robot`) e execute:

```powershell
dotnet run --project .\src\Financial.Robot.Worker\Financial.Robot.Worker.csproj --mode mtapi-tester --port 8230 --symbol WINQ26 --timeframe M1
```

*(Caso você já tenha compilado ou publicado o projeto, basta rodar o executável correspondente passando os mesmos argumentos).*

### 3. O Que Acontecerá?

O `MtApiTesterCliRunner` irá:
1. Conectar-se ao EA na porta `8230`.
2. Receber do EA o estado `IsTesting = true`.
3. Validar de forma silenciosa o comando `BacktestingReady`, que fará o EA desbloquear a execução dos candles no Strategy Tester.
4. Executar chamadas de diagnóstico (`AccountInfo`, `SymbolInfoTick`, `CopyRates`, `SYMBOL_POINT`, `PositionsTotal`, `HistorySelect`).
5. Gerar um arquivo de diagnóstico JSON.

### 4. Resultados

O resultado do teste não tenta comprar ou vender (para evitar loop infinito no tester enquanto configuramos), ele apenas valida a comunicação.

Um arquivo JSON será gerado na pasta `data/mtapi-tests/`, semelhante a:
`mtapi-tester-20260716-123000.json`

Exemplo de conteúdo esperado:
```json
{
  "Connected": true,
  "IsTesting": true,
  "AccountLoaded": true,
  "TickLoaded": true,
  "CandlesLoaded": 10,
  "Errors": [],
  "StartedAtUtc": "2026-07-16T12:30:00Z",
  "FinishedAtUtc": "2026-07-16T12:30:05Z"
}
```

Se o array `Errors` estiver vazio e todos os booleanos estiverem `true`, a ponte de comunicação do MT5 com o Strategy Tester via MtApi5 está 100% operacional.
