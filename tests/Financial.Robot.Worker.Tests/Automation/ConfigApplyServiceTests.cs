using System.Text.Json;
using Financial.Robot.Application.Automation;
using Financial.Robot.Worker.Automation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Financial.Robot.Worker.Tests.Automation;

public sealed class ConfigApplyServiceTests
{
    [Fact]
    public async Task AplicarAsync_DeveCriarBackupEGravarArquivoEmConfig()
    {
        using var temp = new TemporaryDirectory();
        var configAtual = Path.Combine(temp.Path, "config", "WINQ26.config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configAtual)!);
        await File.WriteAllTextAsync(configAtual, "{\"operar\":false}");

        var service = new ConfigApplyService(NullLogger<ConfigApplyService>.Instance);
        var envelope = CriarEnvelope("config/WINQ26.config.json", "{\"operar\":true}");

        var resultado = await service.AplicarAsync(envelope, temp.Path, CancellationToken.None);

        resultado.Status.Should().Be(ConfigApplyStatus.Applied);
        resultado.BackupId.Should().NotBeNullOrWhiteSpace();
        using var jsonAplicado = JsonDocument.Parse(await File.ReadAllTextAsync(configAtual));
        jsonAplicado.RootElement.GetProperty("operar").GetBoolean().Should().BeTrue();
        Directory.GetFiles(Path.Combine(temp.Path, "config", ".backups"), "*.json", SearchOption.AllDirectories)
            .Should().NotBeEmpty();
    }

    [Fact]
    public async Task AplicarAsync_DeveRejeitarPathTraversalAntesDeGravar()
    {
        using var temp = new TemporaryDirectory();
        var service = new ConfigApplyService(NullLogger<ConfigApplyService>.Instance);
        var envelope = CriarEnvelope("../fora.json", "{\"operar\":true}");

        var resultado = await service.AplicarAsync(envelope, temp.Path, CancellationToken.None);

        resultado.Status.Should().Be(ConfigApplyStatus.Rejected);
        File.Exists(Path.Combine(temp.Path, "fora.json")).Should().BeFalse();
    }

    private static ConfigApplyEnvelope CriarEnvelope(string path, string contentJson)
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
