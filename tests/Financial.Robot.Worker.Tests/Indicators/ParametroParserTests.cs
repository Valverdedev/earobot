using Financial.Robot.Worker.Indicators;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace Financial.Robot.Worker.Tests.Indicators;

public class ParametroParserTests
{
    [Fact]
    public void ObterBool_ComBoleanoNativo_RetornaValor()
    {
        var parametros = new Dictionary<string, object> { { "chave", true } };
        ParametroParser.ObterBool(parametros, "chave", false).Should().BeTrue();
    }

    [Fact]
    public void ObterBool_ComJsonElementTrue_RetornaTrue()
    {
        var json = JsonSerializer.SerializeToElement(new { chave = true });
        var element = json.GetProperty("chave");
        var parametros = new Dictionary<string, object> { { "chave", element } };
        ParametroParser.ObterBool(parametros, "chave", false).Should().BeTrue();
    }

    [Fact]
    public void ObterBool_ComStringTrueMaiuscula_RetornaTrue()
    {
        var parametros = new Dictionary<string, object> { { "chave", "True" } };
        ParametroParser.ObterBool(parametros, "chave", false).Should().BeTrue();
    }

    [Fact]
    public void ObterBool_ComStringTrueMinuscula_RetornaTrue()
    {
        var parametros = new Dictionary<string, object> { { "chave", "true" } };
        ParametroParser.ObterBool(parametros, "chave", false).Should().BeTrue();
    }

    [Fact]
    public void ObterBool_ComStringFalse_RetornaFalse()
    {
        var parametros = new Dictionary<string, object> { { "chave", "false" } };
        ParametroParser.ObterBool(parametros, "chave", true).Should().BeFalse();
    }

    [Fact]
    public void ObterBool_ChaveInexistente_RetornaPadrao()
    {
        var parametros = new Dictionary<string, object>();
        ParametroParser.ObterBool(parametros, "chave", true).Should().BeTrue();
    }
}
