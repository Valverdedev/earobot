using Financial.Robot.Application.Automation;

namespace Financial.Robot.Bridge.Api;

public interface IAutomationStore
{
    void SaveExtraction(MarketExtractionEnvelope extraction);
    bool TryGetExtraction(string extractionId, out MarketExtractionEnvelope extraction);
    void SaveMessage(ConfigApplyEnvelope message);
    bool TryGetMessage(string messageId, out ConfigApplyEnvelope message);
    void SaveAck(ConfigApplyAckEnvelope ack);
    bool TryGetAck(string messageId, out ConfigApplyAckEnvelope ack);
}

public sealed class InMemoryAutomationStore : IAutomationStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, MarketExtractionEnvelope> _extractions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConfigApplyEnvelope> _messages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConfigApplyAckEnvelope> _acks = new(StringComparer.OrdinalIgnoreCase);

    public void SaveExtraction(MarketExtractionEnvelope extraction)
    {
        lock (_lock) _extractions[extraction.ExtractionId] = extraction;
    }

    public bool TryGetExtraction(string extractionId, out MarketExtractionEnvelope extraction)
    {
        lock (_lock) return _extractions.TryGetValue(extractionId, out extraction!);
    }

    public void SaveMessage(ConfigApplyEnvelope message)
    {
        lock (_lock) _messages[message.MessageId] = message;
    }

    public bool TryGetMessage(string messageId, out ConfigApplyEnvelope message)
    {
        lock (_lock) return _messages.TryGetValue(messageId, out message!);
    }

    public void SaveAck(ConfigApplyAckEnvelope ack)
    {
        lock (_lock) _acks[ack.MessageId] = ack;
    }

    public bool TryGetAck(string messageId, out ConfigApplyAckEnvelope ack)
    {
        lock (_lock) return _acks.TryGetValue(messageId, out ack!);
    }
}
