using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class ValidadorSaidaDslTests
{
    [Fact]
    public void Validador_PosicaoAsterisco_ForaDeSaida_Rejeitado()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Compra = new BlocoLado
            {
                Setup = new CondicaoDef { Op = ">", A = "posicao.precoEntrada", B = "10" }
            }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("Operando 'posicao.precoEntrada' só é permitido no bloco 'saida'"));
    }

    [Fact]
    public void Validador_RefAsterisco_DentroDeSaida_Rejeitado()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Saida = new SaidaDefinicao
            {
                Compra = new CondicaoDef { Op = ">", A = "ref.foo.abertura", B = "10" }
            }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("Operando 'ref.*' não é permitido no bloco 'saida'"));
    }

    [Fact]
    public void Validador_PadraoExportandoRef_DentroDeSaida_Rejeitado()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Saida = new SaidaDefinicao
            {
                Compra = new CondicaoDef { Padrao = "trendBarAlta", Candle = 1, IdRef = "foo" }
            }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("Exportação de ref ('idRef') não é permitida no bloco 'saida'"));
    }

    [Fact]
    public void Validador_BlocoSaidaVazio_Rejeitado()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Saida = new SaidaDefinicao
            {
                Compra = null,
                Venda = null
            }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().Contain(e => e.Contains("bloco saida: vazio, deve declarar 'compra' e/ou 'venda'"));
    }

    [Fact]
    public void Validador_DefinicaoValidaComSaida_Carrega()
    {
        var def = new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = ">", A = "10", B = "5" } },
            Saida = new SaidaDefinicao
            {
                Compra = new CondicaoDef { Op = ">", A = "posicao.lucroPercentualPreco", B = "1.0" }
            }
        };

        var erros = ValidadorDefinicao.Validar(def);
        erros.Should().BeEmpty();
    }
}
