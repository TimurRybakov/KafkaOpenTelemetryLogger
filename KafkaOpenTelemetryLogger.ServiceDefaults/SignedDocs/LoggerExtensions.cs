using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace KafkaOpenTelemetryLogger.SignedDocs;

public static class LoggerExtensions
{
    public static void LogSignedDocs(this ILogger logger, string? message, params object?[] args)
    {
        List<KeyValuePair<string, object>>? scopeList = null;

        foreach (var item in Baggage.Current)
        {
            // Фильтруем по префиксу (гибко!)
            if (item.Key.StartsWith("SignedDoc.", StringComparison.Ordinal))
            {
                scopeList ??= [];

                if (item.Key.EndsWith("Id", StringComparison.Ordinal))
                {
                    if (int.TryParse(item.Value, out int intValue))
                    {
                        scopeList.Add(new("SignedDocId", intValue));
                    }
                }
                else if (item.Key.EndsWith("ParentId", StringComparison.Ordinal))
                {
                    if (int.TryParse(item.Value, out int intValue))
                    {
                        scopeList.Add(new("SignedDocParentId", intValue));
                    }
                }
            }
        }

        if (scopeList is not null)
        {
            using var scope = logger.BeginScope(scopeList);

            logger.LogInformation(message, args);

            return;
        }

        logger.LogInformation(message, args);
    }
}
