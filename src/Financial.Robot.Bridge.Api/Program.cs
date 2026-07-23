using Financial.Robot.Application.Automation;
using Financial.Robot.Bridge.Api;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BridgeApiOptions>(builder.Configuration.GetSection("BridgeApi"));
builder.Services.AddSingleton<IAutomationStore, InMemoryAutomationStore>();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("local-test", policy => policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .SetIsOriginAllowed(_ => true)
        .AllowCredentials());
});

var useFakeHermes = builder.Configuration.GetValue<bool>("BridgeApi:UseFakeHermes", true);
if (useFakeHermes)
{
    builder.Services.AddSingleton<IHermesAnalysisClient, FakeHermesAnalysisClient>();
}
else
{
    builder.Services.AddHttpClient<IHermesAnalysisClient, HttpHermesAnalysisClient>();
}

var app = builder.Build();
app.UseCors("local-test");

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "Financial.Robot.Bridge.Api",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/extractions/{extractionId}", (string extractionId, IAutomationStore store) =>
    store.TryGetExtraction(extractionId, out var extraction)
        ? Results.Ok(extraction)
        : Results.NotFound(new { error = "extraction_not_found" }));

app.MapGet("/api/messages/{messageId}", (string messageId, IAutomationStore store) =>
    store.TryGetMessage(messageId, out var message)
        ? Results.Ok(message)
        : Results.NotFound(new { error = "message_not_found" }));

app.MapGet("/api/acks/{messageId}", (string messageId, IAutomationStore store) =>
    store.TryGetAck(messageId, out var ack)
        ? Results.Ok(ack)
        : Results.NotFound(new { error = "ack_not_found" }));

app.MapPost("/api/extractions", async (
    MarketExtractionEnvelope extraction,
    HttpContext httpContext,
    IAutomationStore store,
    IHermesAnalysisClient hermes,
    IHubContext<EarobotHub> hub,
    IOptions<BridgeApiOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!DeviceTokenValid(httpContext, options.Value))
        return Results.Unauthorized();

    var extractionErrors = AutomationContractValidator.ValidarExtracao(extraction);
    if (extractionErrors.Count > 0)
        return Results.BadRequest(new { errors = extractionErrors });

    store.SaveExtraction(extraction);

    var apply = await hermes.AnalyzeExtractionAsync(extraction, cancellationToken);
    if (apply is null)
        return Results.Accepted($"/api/extractions/{extraction.ExtractionId}", new
        {
            extraction.ExtractionId,
            status = "analysis.no_change"
        });

    var applyErrors = AutomationContractValidator.ValidarApply(apply);
    if (applyErrors.Count > 0)
        return Results.BadRequest(new { errors = applyErrors });

    store.SaveMessage(apply);
    await PublishConfigApplyAsync(hub, apply, cancellationToken);

    return Results.Accepted($"/api/messages/{apply.MessageId}", new
    {
        extraction.ExtractionId,
        messageId = apply.MessageId,
        status = "config_update_requested",
        configApply = apply
    });
});

app.MapPost("/api/hermes/strategies", async (
    ConfigApplyEnvelope envelope,
    HttpContext httpContext,
    IAutomationStore store,
    IHubContext<EarobotHub> hub,
    IOptions<BridgeApiOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!DeviceTokenValid(httpContext, options.Value))
        return Results.Unauthorized();

    var errors = AutomationContractValidator.ValidarApply(envelope);
    if (errors.Count > 0)
        return Results.BadRequest(new { errors });

    store.SaveMessage(envelope);
    await PublishConfigApplyAsync(hub, envelope, cancellationToken);

    return Results.Accepted($"/api/messages/{envelope.MessageId}", new
    {
        messageId = envelope.MessageId,
        status = "config_update_requested",
        configApply = envelope
    });
});

app.MapPost("/api/test/config-apply", async (
    ConfigApplyEnvelope envelope,
    IAutomationStore store,
    IHubContext<EarobotHub> hub,
    CancellationToken cancellationToken) =>
{
    var errors = AutomationContractValidator.ValidarApply(envelope);
    if (errors.Count > 0)
        return Results.BadRequest(new { errors });

    store.SaveMessage(envelope);
    await PublishConfigApplyAsync(hub, envelope, cancellationToken);
    return Results.Ok(new { messageId = envelope.MessageId, status = "published" });
});

app.MapHub<EarobotHub>("/hubs/earobot");

app.Run();

static bool DeviceTokenValid(HttpContext context, BridgeApiOptions options)
{
    if (!options.RequireDeviceToken)
        return true;

    if (string.IsNullOrWhiteSpace(options.DeviceToken))
        return false;

    var headerToken = context.Request.Headers["X-Device-Token"].ToString();
    if (string.Equals(headerToken, options.DeviceToken, StringComparison.Ordinal))
        return true;

    var auth = context.Request.Headers.Authorization.ToString();
    const string bearer = "Bearer ";
    return auth.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
        && string.Equals(auth[bearer.Length..].Trim(), options.DeviceToken, StringComparison.Ordinal);
}

static Task PublishConfigApplyAsync(IHubContext<EarobotHub> hub, ConfigApplyEnvelope envelope, CancellationToken cancellationToken)
{
    return hub.Clients.All.SendAsync("ConfigUpdateRequested", envelope, cancellationToken);
}

public partial class Program;
