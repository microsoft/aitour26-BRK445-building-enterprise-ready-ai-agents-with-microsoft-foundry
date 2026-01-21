using Azure.Identity;
using Microsoft.Extensions.AI;
using DataService.Endpoints;
using DataService.Memory;
using ZavaDatabaseInitialization;
using Microsoft.EntityFrameworkCore;
using ZavaMAFFoundry;
using ZavaMAFOllama;

var builder = WebApplication.CreateBuilder(args);

// Disable Globalization Invariant Mode
Environment.SetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "false");

// add aspire service defaults
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

// Add DbContext service
var connectionString = builder.Configuration.GetConnectionString("productsDb") ?? "Data Source=Database.db";
builder.Services.AddDbContext<Context>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<IConfiguration>(sp =>
{
    return builder.Configuration;
});

// Register MAF Microsoft Foundry Agents, chat client and embedding generator
builder.AddMAFFoundryAgents();

// register MAF Ollama Agents, chat client and embedding generator
builder.AddMAFOllamaAgents();

// add memory context
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetService<ILogger<Program>>();
    logger.LogInformation("Creating memory context");

    var chatClient = sp.GetService<IChatClient>();
    var embeddingGenerator = sp.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
    var ollamaChatClient = sp.GetRequiredKeyedService<IChatClient>("ollama");
    var ollamaEmbeddingGenerator = sp.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("ollama");

    return new MemoryContext(logger, 
        chatClient, 
        embeddingGenerator, 
        ollamaChatClient,
        ollamaEmbeddingGenerator);
});

// Add services to the container.
var app = builder.Build();

// aspire map default endpoints
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapProductEndpoints();
app.MapDataEndpoints();

app.UseStaticFiles();

AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);

// manage db
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<Context>();
    try
    {
        app.Logger.LogInformation("Ensure database created");
        context.Database.EnsureCreated();
    }
    catch (Exception exc)
    {
        app.Logger.LogError(exc, "Error creating database");
    }
    DbInitializer.Initialize(context);

    app.Logger.LogInformation("Start fill products in vector db");
    var memoryContext = app.Services.GetRequiredService<MemoryContext>();
    await memoryContext.InitMemoryContextAsync(context);
    app.Logger.LogInformation("Done fill products in vector db");
}

app.Run();