using OpenTelemetry;

namespace KafkaOpenTelemetryLogger.SignedDocs;

public static class SignedDocsOtel
{
    public static IDisposable BeginScope(SignedDocContext context)
    {
        var baggage = Baggage.Current;

        baggage = baggage.SetBaggage("SignedDoc.Id", context.SignedDocId.ToString());
        if (context.SignedDocParentId is not null)
            baggage = baggage.SetBaggage("SignedDoc.ParentId", context.SignedDocParentId.ToString());

        var original = Baggage.Current;
        Baggage.Current = baggage;
        return new BaggageResetOnDispose(original);
    }

    private sealed class BaggageResetOnDispose : IDisposable
    {
        private readonly Baggage _original;

        public BaggageResetOnDispose(Baggage original) => _original = original;

        public void Dispose() => Baggage.Current = _original;
    }
}
