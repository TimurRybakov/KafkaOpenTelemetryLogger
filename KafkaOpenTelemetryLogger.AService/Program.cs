using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry;

System.Console.OutputEncoding = System.Text.Encoding.UTF8;
System.Console.InputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/test", (ILogger<Program> logger) =>
{
    using var activity = new Activity("logging").Start();

    using var _ = SignedDocsOtel.BeginScope(new SignedDocContext(123, null));

    // Обычный лог
    logger.LogTrace("Обычный лог из AService");
    logger.LogDebug("Обычный лог из AService");
    logger.LogInformation("Обычный лог из AService");
    logger.LogWarning("Обычный лог из AService");
    logger.LogError("Обычный лог из AService");
    logger.LogCritical("Обычный лог из AService");

    // Лог в DB
    logger.LogSignedDocs("Чувствительная операция: логируем в SignedDocs");

    return Results.Ok();
})
.WithName("TestLogger")
.WithOpenApi();

app.Run();

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

public sealed record SignedDocContext(int SignedDocId, int? SignedDocParentId);
