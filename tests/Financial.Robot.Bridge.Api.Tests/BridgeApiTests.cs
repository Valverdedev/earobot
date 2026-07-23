using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Financial.Robot.Application.Automation;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Financial.Robot.Bridge.Api.Tests;

public sealed class BridgeApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BridgeApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BridgeApi:UseFakeHermes", "true");
            builder.UseSetting("BridgeApi:RequireDeviceToken", "false");
        });
    }

    [Fact]
    public async Task Health_DeveRetornarOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task PostExtraction_DeveValidarAcionarHermesFakeEPersistirMensagem()
    {
        var client = _factory.CreateClient();
        var extraction = CriarExtracao("ext-price-above", "price_above");

        var response = await client.PostAsJsonAsync("/api/extractions", extraction);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("extractionId").GetString().Should().Be("ext-price-above");
        var apply = json.GetProperty("configApply");
        apply.GetProperty("type").GetString().Should().Be(AutomationMessageTypes.ConfigApply);
        apply.GetProperty("applyMode").GetString().Should().Be(ConfigApplyModes.Automatic);
        apply.GetProperty("files")[0].GetProperty("path").GetString().Should().Be("config/WINQ26.config.json");

        var messageId = apply.GetProperty("messageId").GetString();
        var stored = await client.GetAsync($"/api/messages/{messageId}");
        stored.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostExtraction_DeveRejeitarContratoInvalido()
    {
        var client = _factory.CreateClient();
        var invalid = CriarExtracao("ext-invalid", "price_above") with
        {
            Trigger = new AutomationTrigger("price_above")
        };

        var response = await client.PostAsJsonAsync("/api/extractions", invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(e => e!.Contains("price e threshold"));
    }

    [Fact]
    public async Task PostStrategy_DevePublicarConfigUpdateRequestedNoSignalR()
    {
        var client = _factory.CreateClient();
        using var received = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var tcs = new TaskCompletionSource<ConfigApplyEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/earobot"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        connection.On<ConfigApplyEnvelope>("ConfigUpdateRequested", envelope => tcs.TrySetResult(envelope));
        await connection.StartAsync(received.Token);

        var envelope = CriarApply("msg-signalr");
        var response = await client.PostAsJsonAsync("/api/hermes/strategies", envelope, received.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var delivered = await tcs.Task.WaitAsync(received.Token);
        delivered.MessageId.Should().Be("msg-signalr");
        delivered.Type.Should().Be(AutomationMessageTypes.ConfigApply);

        await connection.DisposeAsync();
    }

    private static MarketExtractionEnvelope CriarExtracao(string id, string triggerType) => new(
        "1.0",
        id,
        "financial.robot.tests",
        DateTimeOffset.UtcNow,
        new AutomationTrigger(triggerType, Price: 129100, Threshold: 129000),
        new AutomationTarget("mt5-principal", "WINQ26", "WINQ26", "simulation", ["M1", "M5"]),
        JsonDocument.Parse("{\"price\":129100}").RootElement.Clone(),
        JsonDocument.Parse("{\"candles\":[],\"indicators\":{},\"positions\":[],\"orders\":[]}").RootElement.Clone());

    private static ConfigApplyEnvelope CriarApply(string messageId) => new(
        "1.0",
        messageId,
        "corr-signalr",
        AutomationMessageTypes.ConfigApply,
        new AutomationTarget("mt5-principal", "WINQ26", "WINQ26", "simulation", ["M1", "M5"]),
        [
            new ConfigApplyFile(
                "config/WINQ26.config.json",
                ConfigApplyOperations.Merge,
                JsonDocument.Parse("{\"operar\":true}").RootElement.Clone())
        ],
        ConfigApplyModes.Automatic,
        new RollbackOptions(true, "auto"));
}
