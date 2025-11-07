using KafkaOpenTelemetryLogger.SignedDocs;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

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
    // Обычный лог
    logger.LogTrace("Trace лог из BService");
    logger.LogDebug("Debug лог из BService");
    logger.LogInformation("Information лог из BService");
    logger.LogWarning("Warning лог из BService");
    logger.LogError("Error лог из BService");
    logger.LogCritical("Critical лог из BService");

    // Лог в DB
    logger.LogSignedDocs("Логируем в SignedDocs в BService");

    LogMethod();

    return Results.Ok();

    void LogMethod()
    {
        logger.LogSignedDocs("Логируем в SignedDocs внутри метода LogMethod() в BService");
        logger.LogInformation("Был лог в SignedDocs в LogMethod сервиса BService");
    }
})
.WithName("TestLogger")
.WithOpenApi();

app.Run();
