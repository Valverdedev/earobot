using Financial.Robot.Application.Backtesting.Interfaces;
using Financial.Robot.Application.Backtesting.Models;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Application.Backtesting.Services;

public class ExecutionSimulator : IBacktestExecutionSimulator
{
    private double _capitalInicial;
    private double _spreadFixo;
    private double _slippageFixo;
    private double _comissaoPorContrato;

    private double _equity;
    private double _maxEquity;
    private double _maxDrawdownPercentual;
    private int _sequenciaPerdasAtual;
    private int _maiorSequenciaPerdas;

    private readonly List<BacktestEquityPoint> _curvaEquity = new();
    private readonly List<BacktestTrade> _trades = new();
    
    // Representa a posição aberta. Por enquanto, a estratégia do robô não faz partial closes ou grid, ela entra e sai com 1 trade.
    private OpenPosition? _posicaoAtual;

    public void Initialize(double capitalInicial, double spreadFixo, double slippageFixo, double comissaoPorContrato)
    {
        _capitalInicial = capitalInicial;
        _spreadFixo = spreadFixo;
        _slippageFixo = slippageFixo;
        _comissaoPorContrato = comissaoPorContrato;
        
        _equity = capitalInicial;
        _maxEquity = capitalInicial;
        _maxDrawdownPercentual = 0;
        _sequenciaPerdasAtual = 0;
        _maiorSequenciaPerdas = 0;

        _curvaEquity.Clear();
        _trades.Clear();
        _posicaoAtual = null;

        _curvaEquity.Add(new BacktestEquityPoint(DateTime.MinValue, _equity));
    }

    public void ExecuteOrder(LadoOrdem lado, double volume, double? stopLoss, double? takeProfit, CandleMt5 executionCandle)
    {
        if (_posicaoAtual != null)
        {
            // O robô atual tenta operar uma ordem por vez, dependendo da config.
            return; 
        }

        double precoBase = executionCandle.Fechamento; // Assumimos que o sinal ocorre no fechamento do candle
        double entryPrice = lado == LadoOrdem.Compra 
            ? precoBase + _spreadFixo + _slippageFixo 
            : precoBase - _spreadFixo - _slippageFixo;

        _posicaoAtual = new OpenPosition
        {
            Lado = lado,
            Volume = volume,
            EntryPrice = entryPrice,
            StopLoss = stopLoss,
            TakeProfit = takeProfit,
            EntryTime = executionCandle.Tempo
        };
    }

    public void ProcessCandle(CandleMt5 candle)
    {
        if (_posicaoAtual == null)
            return;

        var pos = _posicaoAtual;
        bool bateuSl = false;
        bool bateuTp = false;

        if (pos.Lado == LadoOrdem.Compra)
        {
            // Para compra: Low do candle pode tocar o Stop Loss, High pode tocar o Take Profit
            if (pos.StopLoss.HasValue && candle.Minimo <= pos.StopLoss.Value) bateuSl = true;
            if (pos.TakeProfit.HasValue && candle.Maximo >= pos.TakeProfit.Value) bateuTp = true;
        }
        else
        {
            // Para venda: High do candle pode tocar o Stop Loss, Low pode tocar o Take Profit
            if (pos.StopLoss.HasValue && candle.Maximo >= pos.StopLoss.Value) bateuSl = true;
            if (pos.TakeProfit.HasValue && candle.Minimo <= pos.TakeProfit.Value) bateuTp = true;
        }

        if (bateuSl && bateuTp)
        {
            // Lógica Conservadora Intra-Candle
            ClosePosition(pos.StopLoss!.Value, candle.Tempo, "StopLoss_ConservativeCollision");
        }
        else if (bateuSl)
        {
            ClosePosition(pos.StopLoss!.Value, candle.Tempo, "StopLoss");
        }
        else if (bateuTp)
        {
            ClosePosition(pos.TakeProfit!.Value, candle.Tempo, "TakeProfit");
        }
    }

    public void CloseAllPositions(CandleMt5 finalCandle, string motivo)
    {
        if (_posicaoAtual != null)
        {
            ClosePosition(finalCandle.Fechamento, finalCandle.Tempo, motivo);
        }
    }

    private void ClosePosition(double baseExitPrice, DateTime exitTime, string motivo)
    {
        if (_posicaoAtual == null) return;
        var pos = _posicaoAtual;

        double exitPrice = pos.Lado == LadoOrdem.Compra
            ? baseExitPrice - _slippageFixo // Vende para fechar a compra, paga slippage
            : baseExitPrice + _slippageFixo; // Compra para fechar a venda, paga slippage

        double pontos = pos.Lado == LadoOrdem.Compra
            ? exitPrice - pos.EntryPrice
            : pos.EntryPrice - exitPrice;

        // O pnl na conta real depende do tick value (valor por ponto).
        // Simplificando o simulador: Pnl = pontos * volume (exemplo para B3/Forex simples onde 1 pt = 1 unidade, ou volume já reflete lote cheio).
        // Se quisermos exatidão do B3 (Mini índice onde lote 1 = R$ 0.20 por ponto), vamos expor isso ou requerer que o volume já seja financeiro (Multiplier).
        // No MT5 o lote de 1.0 no Win são 1 contrato que paga 0.20BRL/pt.
        // Vamos aplicar a comissão configurada pelo Request.
        
        // Pnl bruto = pontos * volume. Assumindo modelo direto por enquanto.
        double pnlBruto = pontos * pos.Volume;
        double custoComissoes = _comissaoPorContrato * pos.Volume;
        double pnlLiquido = pnlBruto - custoComissoes;

        _equity += pnlLiquido;

        if (_equity > _maxEquity)
        {
            _maxEquity = _equity;
        }

        double currentDrawdown = (_maxEquity - _equity) / _maxEquity * 100.0;
        if (currentDrawdown > _maxDrawdownPercentual)
        {
            _maxDrawdownPercentual = currentDrawdown;
        }

        if (pnlLiquido < 0)
        {
            _sequenciaPerdasAtual++;
            if (_sequenciaPerdasAtual > _maiorSequenciaPerdas)
                _maiorSequenciaPerdas = _sequenciaPerdasAtual;
        }
        else
        {
            _sequenciaPerdasAtual = 0;
        }

        _trades.Add(new BacktestTrade(
            EntryTime: pos.EntryTime,
            ExitTime: exitTime,
            Lado: pos.Lado,
            Volume: pos.Volume,
            EntryPrice: pos.EntryPrice,
            ExitPrice: exitPrice,
            PnlLiquido: pnlLiquido,
            MotivoSaida: motivo
        ));

        _curvaEquity.Add(new BacktestEquityPoint(exitTime, _equity));

        _posicaoAtual = null; // Libera
    }

    public BacktestMetrics GetMetrics()
    {
        int total = _trades.Count;
        int win = _trades.Count(t => t.PnlLiquido > 0);
        int loss = total - win;
        double winRate = total > 0 ? (double)win / total : 0;
        double netProfit = _trades.Sum(t => t.PnlLiquido);
        
        double grossProfit = _trades.Where(t => t.PnlLiquido > 0).Sum(t => t.PnlLiquido);
        double grossLoss = Math.Abs(_trades.Where(t => t.PnlLiquido <= 0).Sum(t => t.PnlLiquido));
        double profitFactor = grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? double.PositiveInfinity : 0);

        double payoffMedio = win > 0 && loss > 0 
            ? (grossProfit / win) / (grossLoss / loss) 
            : 0;

        return new BacktestMetrics(
            TotalTrades: total,
            TradesComLucro: win,
            TradesComPrejuizo: loss,
            TaxaAcerto: winRate,
            LucroLiquidoTotal: netProfit,
            ProfitFactor: profitFactor,
            DrawdownMaximoPercentual: _maxDrawdownPercentual,
            PayoffMedio: payoffMedio,
            MaiorSequenciaPerdas: _maiorSequenciaPerdas,
            CurvaEquity: _curvaEquity,
            Trades: _trades
        );
    }

    private class OpenPosition
    {
        public LadoOrdem Lado { get; set; }
        public double Volume { get; set; }
        public double EntryPrice { get; set; }
        public double? StopLoss { get; set; }
        public double? TakeProfit { get; set; }
        public DateTime EntryTime { get; set; }
    }
}
