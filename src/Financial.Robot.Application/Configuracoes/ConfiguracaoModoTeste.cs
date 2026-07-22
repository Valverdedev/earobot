namespace Financial.Robot.Application.Configuracoes;

/// <summary>
/// Configuração fortemente tipada para o arquivo test-mode.config.json.
/// Bind via IOptions&lt;ConfiguracaoModoTeste&gt;.
/// </summary>
public sealed class ConfiguracaoModoTeste
{
    /// <summary>Nome da seção no arquivo de configuração.</summary>
    public const string NomeSeccao = "ConfiguracaoModoTeste";

    /// <summary>Habilita o modo teste.</summary>
    public bool ModoTeste { get; set; }

    /// <summary>Configurações de conexão com o MT5.</summary>
    public ConfiguracaoMtApi MtApi { get; set; } = new();

    /// <summary>Parâmetros da sequência de teste.</summary>
    public ConfiguracaoSequenciaTeste SequenciaTeste { get; set; } = new();

    /// <summary>Configurações de log.</summary>
    public ConfiguracaoLog Log { get; set; } = new();
}

/// <summary>Parâmetros de conexão com a API MT5.</summary>
public sealed class ConfiguracaoMtApi
{
    /// <summary>Host do servidor MT5 (padrão: localhost).</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Porta do EA bridge (padrão: 8228).</summary>
    public int Porta { get; set; } = 8228;

    /// <summary>Timeout de conexão em segundos.</summary>
    public int TimeoutConexaoSegundos { get; set; } = 15;
}

/// <summary>Parâmetros da sequência de ações do modo teste.</summary>
public sealed class ConfiguracaoSequenciaTeste
{
    /// <summary>Símbolo a operar (ex: EURUSD).</summary>
    public string Simbolo { get; set; } = "EURUSD";

    /// <summary>Volume em lotes da ordem de teste.</summary>
    public double Volume { get; set; } = 0.01;

    /// <summary>Tipo da ordem a mercado (BUY ou SELL).</summary>
    public string TipoOrdem { get; set; } = "BUY";

    /// <summary>Stop Loss inicial em pips.</summary>
    public int StopLossPips { get; set; } = 20;

    /// <summary>Take Profit inicial em pips.</summary>
    public int TakeProfitPips { get; set; } = 20;

    /// <summary>Novo Stop Loss após modificação (pips).</summary>
    public int StopLossModificadoPips { get; set; } = 30;

    /// <summary>Novo Take Profit após modificação (pips).</summary>
    public int TakeProfitModificadoPips { get; set; } = 30;

    /// <summary>Configuração da ordem pendente.</summary>
    public ConfiguracaoOrdemPendente OrdemPendente { get; set; } = new();

    /// <summary>Delay entre cada ação da sequência em segundos.</summary>
    public int DelayEntreAcoesSegundos { get; set; } = 5;
}

/// <summary>Parâmetros da ordem pendente no modo teste.</summary>
public sealed class ConfiguracaoOrdemPendente
{
    /// <summary>Tipo da ordem pendente (ex: BUY_LIMIT).</summary>
    public string Tipo { get; set; } = "BUY_LIMIT";

    /// <summary>Distância em pips do preço atual para a ordem pendente.</summary>
    public int DistanciaPips { get; set; } = 50;

    /// <summary>Stop Loss da ordem pendente em pips.</summary>
    public int StopLossPips { get; set; } = 20;

    /// <summary>Take Profit da ordem pendente em pips.</summary>
    public int TakeProfitPips { get; set; } = 20;
}

/// <summary>Configurações de saída de log.</summary>
public sealed class ConfiguracaoLog
{
    /// <summary>Caminho do arquivo de log (suporta data rolling).</summary>
    public string CaminhoArquivo { get; set; } = "logs/test-mode-.log";

    /// <summary>Nível mínimo de log (Information, Debug, Warning, Error).</summary>
    public string NivelMinimo { get; set; } = "Information";
}
