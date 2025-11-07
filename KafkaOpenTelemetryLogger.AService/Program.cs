using System.Diagnostics;
using KafkaOpenTelemetryLogger.SignedDocs;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<BService>((serviceProvider, client) =>
{
    client.BaseAddress = new Uri("https://b-service");
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/test", async (ILogger<Program> logger, BService bService) =>
{
    using var activity = new Activity("logging").Start();

    using var _ = SignedDocsOtel.BeginScope(new SignedDocContext(123, null));

    // Обычный лог
    logger.LogTrace("Trace лог из AService");
    logger.LogDebug("Debug лог из AService");
    logger.LogInformation("Information лог из AService");
    logger.LogWarning("Warning лог из AService");
    logger.LogError("Error лог из AService");
    logger.LogCritical("Critical лог из AService");


    // Лог в DB
    logger.LogSignedDocs("Логируем в SignedDocs в AService");

    var response = await CallTestOnBService();

    return response;

    async Task<HttpResponseMessage> CallTestOnBService()
    {
        logger.LogSignedDocs("Логируем в SignedDocs внутри метода CallTestOnBService() в AService");
        logger.LogInformation("Был лог в SignedDocs в CallTestOnBService сервиса AService");

        var response = await bService.GetTestAsync();

        logger.LogInformation("Был вызов http get /test сервиса BService из сервиса AService");

        return response;
    }
})
.WithName("TestLogger")
.WithOpenApi();

app.Run();

public sealed class BService(HttpClient client)
{
    public async Task<HttpResponseMessage> GetTestAsync()
    {
        var response = await client.GetAsync($"test/");

        return response;
    }
}
