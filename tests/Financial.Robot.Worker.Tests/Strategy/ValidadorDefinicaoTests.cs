using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class ValidadorDefinicaoTests
{
    [Fact]
    public void Validar_DeveRejeitarFiltroDesconhecido()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Teste",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = ">", A = "1", B = "0" } },
            Filtros = [new FiltroDef { Tipo = "filtroDesconhecido" }]
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("Tipo de filtro desconhecido"));
    }

    [Fact]
    public void Validar_AlinhadosDeveExigirOrdem()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Teste",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = "alinhados", Serie = ["1", "2"] } }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("'ordem' ('crescente' ou 'decrescente') é obrigatório"));
    }

    [Fact]
    public void Validar_SequenciaDeveExigirLado()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Teste",
            Compra = new BlocoLado { Setup = new CondicaoDef { Padrao = "sequencia", Candles = 3 } }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("'lado' ('alta' ou 'baixa') e 'candles' são obrigatórios"));
    }

    [Fact]
    public void Validar_PertoDeDeveExigirToleranciaPreco()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Teste",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = "pertoDe", A = "1", B = "2" } }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("'toleranciaPreco' é obrigatório"));
    }

    [Fact]
    public void Validar_DistanciaMinimaDeveExigirValorPreco()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Teste",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = "distanciaMinima", A = "1", B = "2" } }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("'valorPreco' é obrigatório"));
    }

    [Fact]
    public void Validar_DevePassarDefinicaoValida()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Valido",
            Filtros = [new FiltroDef { Tipo = "spreadMaximo", ValorPreco = 100 }],
            Compra = new BlocoLado
            {
                Setup = new CondicaoDef { Op = ">", A = "tick.ask", B = "10" },
                Stop = new StopDef { Tipo = "valorOperando", A = "10", BufferPreco = 5 }
            }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().BeEmpty();
    }
    [Fact]
    public void Validar_DeveRejeitarPosicaoNaEntrada()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Teste",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = ">", A = "posicao.lucroBruto", B = "100" } }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("só é permitido no bloco 'saida'"));
    }

    [Fact]
    public void Validar_DeveRejeitarTickNaSaida()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Teste",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = "==", A = "1", B = "1" } },
            Saida = new SaidaDefinicao
            {
                Compra = new CondicaoDef { Op = ">", A = "tick.bid", B = "ema(9)" }
            }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("Operando 'tick.*' não é permitido no bloco 'saida'"));
    }

    [Fact]
    public void Validar_DeveAceitarPosicaoNaSaida()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Nome = "Teste",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = ">", A = "ema(9)", B = "ema(21)" } },
            Saida = new SaidaDefinicao
            {
                Compra = new CondicaoDef { Op = ">", A = "posicao.lucroBruto", B = "100" }
            }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().BeEmpty();
    }
}
