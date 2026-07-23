using System.Text.Json;
using Financial.Robot.Application.Automation;
using FluentAssertions;

namespace Financial.Robot.Application.Tests.Automation;

public sealed class AutomationContractValidatorTests
{
    [Fact]
    public void ValidarExtracao_ComGatilhoPrecoAcima_DeveAceitar()
    {
        var extracao = new MarketExtractionEnvelope(
            SchemaVersion: "1.0",
            ExtractionId: "ext_01",
            Source: "financial.robot",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Trigger: new AutomationTrigger("price_above", Price: 129100, Threshold: 129000),
            Target: new AutomationTarget("mt5-principal", "WINQ26", "WINQ26", "live", ["M5", "M1"]),
            MarketContext: JsonDocument.Parse("{\"price\":129100}").RootElement,
            Raw: JsonDocument.Parse("{\"positions\":[]}").RootElement);

        var erros = AutomationContractValidator.ValidarExtracao(extracao);

        erros.Should().BeEmpty();
    }

    [Fact]
    public void ValidarExtracao_ComOperarLive_DeveAceitarAutomacaoTotal()
    {
        var extracao = CriarExtracao(triggerType: "order_closed");

        var erros = AutomationContractValidator.ValidarExtracao(extracao);

        erros.Should().BeEmpty();
    }

    [Fact]
    public void ValidarApply_ComOperarTrue_DeveAceitarQuandoHaGuardrails()
    {
        var apply = CriarApply("config/WINQ26.config.json", "{\"operar\":true,\"guardrails\":{\"exigirStopLoss\":true}}");

        var erros = AutomationContractValidator.ValidarApply(apply);

        erros.Should().BeEmpty();
    }

    [Fact]
    public void ValidarApply_ComPathTraversal_DeveRejeitar()
    {
        var apply = CriarApply("../secrets.json", "{\"operar\":true}");

        var erros = AutomationContractValidator.ValidarApply(apply);

        erros.Should().Contain(e => e.Contains("path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidarApply_ComArquivoForaDeConfig_DeveRejeitar()
    {
        var apply = CriarApply("logs/financial.log", "{}");

        var erros = AutomationContractValidator.ValidarApply(apply);

        erros.Should().Contain(e => e.Contains("config/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidarApply_ComEstrategiaSemSchema_DeveRejeitar()
    {
        var apply = CriarApply("config/WINQ26-auto.estrategia.json", "{\"nome\":\"Auto\"}");

        var erros = AutomationContractValidator.ValidarApply(apply);

        erros.Should().Contain(e => e.Contains("$schemaVersion", StringComparison.OrdinalIgnoreCase));
    }

    private static MarketExtractionEnvelope CriarExtracao(string triggerType)
    {
        return new MarketExtractionEnvelope(
            SchemaVersion: "1.0",
            ExtractionId: "ext_01",
            Source: "financial.robot",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Trigger: new AutomationTrigger(triggerType, Ticket: 123456, Result: "take_profit"),
            Target: new AutomationTarget("mt5-principal", "WINQ26", "WINQ26", "live", ["M5"]),
            MarketContext: JsonDocument.Parse("{\"trend\":\"alta\"}").RootElement,
            Raw: JsonDocument.Parse("{\"orders\":[]}").RootElement);
    }

    private static ConfigApplyEnvelope CriarApply(string path, string contentJson)
    {
        return new ConfigApplyEnvelope(
            SchemaVersion: "1.0",
            MessageId: "msg_01",
            CorrelationId: "ext_01",
            Type: AutomationMessageTypes.ConfigApply,
            Target: new AutomationTarget("mt5-principal", "WINQ26", "WINQ26", "live", ["M5"]),
            Files:
            [
                new ConfigApplyFile(
                    Path: path,
                    Operation: ConfigApplyOperations.Replace,
                    Content: JsonDocument.Parse(contentJson).RootElement)
            ],
            ApplyMode: ConfigApplyModes.Automatic,
            Rollback: new RollbackOptions(true, "auto"));
    }
}
