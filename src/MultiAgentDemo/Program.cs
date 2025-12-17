using Microsoft.Agents.AI.DevUI;
using MultiAgentDemo.Services;
using ZavaMAFLocal;
using ZavaMAFFoundry;
using DataServiceClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add DataServiceClient for accessing DataService endpoints
builder.Services.AddDataServiceClient("https+http://dataservice", builder.Environment.IsDevelopment());

// Register MAF Foundry agents (Microsoft Foundry)
builder.AddMAFFoundryAgents();

// Register MAF agent providers using new extension methods
var microsoftFoundryCnnString = builder.Configuration.GetConnectionString("microsoftfoundrycnnstring");
var chatDeploymentName = builder.Configuration["AI_ChatDeploymentName"] ?? "gpt-5-mini";

// Register MAF Local agents (locally created with IChatClient)
// Register IChatClient for MAF Local agents
builder.AddAzureOpenAIClient(connectionName: "microsoftfoundrycnnstring",
    configureSettings: settings =>
    {
        if (string.IsNullOrEmpty(settings.Key))
        {
            settings.Credential = new Azure.Identity.DefaultAzureCredential();
        }
    }).AddChatClient(chatDeploymentName);

builder.AddMAFLocalAgents();

// add workflows
builder.AddMAFLocalWorkflows();

// Register HTTP clients for external services (used by LLM direct call and DirectCall modes)
RegisterHttpClients(builder);

// Register orchestration services for LLM mode
RegisterOrchestrationServices(builder);

// Register services for OpenAI responses and conversations (required for DevUI)
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

// Add DevUI for agent debugging and visualization
builder.AddDevUI();

var app = builder.Build();

app.MapDefaultEndpoints();

// Health logging endpoint for troubleshooting
app.MapGet("/health/log", (ILogger<Program> logger, IConfiguration config) =>
{
    logger.LogInformation("MultiAgentDemo health/log requested");
    var appInsights = config["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? config["appinsights"] ?? "<not-set>";
    var foundryCnn = config.GetConnectionString("microsoftfoundrycnnstring") ?? "<not-set>";
    var foundryProject = config.GetConnectionString("microsoftfoundryproject") ?? "<not-set>";
    var env = config["ASPNETCORE_ENVIRONMENT"] ?? "<unknown>";

    logger.LogInformation("MultiAgentDemo Config - Env: {Env}, AppInsights: {AppInsights}, FoundryCnn: {FoundryCnn}, FoundryProject: {FoundryProject}", env, appInsights, foundryCnn, foundryProject);

    return Results.Ok(new {
        service = "multiagentdemo",
        env,
        appInsights = string.IsNullOrEmpty(appInsights) ? "<not-set>" : "set",
        microsoftFoundryConnection = string.IsNullOrEmpty(foundryCnn) ? "<not-set>" : "set",
        microsoftFoundryProject = string.IsNullOrEmpty(foundryProject) ? "<not-set>" : "set"
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Map DevUI endpoints for agent debugging (development only)
    app.MapOpenAIResponses();
    app.MapOpenAIConversations();
    app.MapDevUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Registers HTTP clients for external service communication (LLM direct call and DirectCall modes).
/// </summary>
static void RegisterHttpClients(WebApplicationBuilder builder)
{
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddHttpClient<InventoryAgentService>(
            client => client.BaseAddress = new Uri("https+http://inventoryservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        builder.Services.AddHttpClient<MatchmakingAgentService>(
            client => client.BaseAddress = new Uri("https+http://matchmakingservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        builder.Services.AddHttpClient<LocationAgentService>(
            client => client.BaseAddress = new Uri("https+http://locationservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        builder.Services.AddHttpClient<NavigationAgentService>(
            client => client.BaseAddress = new Uri("https+http://navigationservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
    }
    else
    {
        builder.Services.AddHttpClient<InventoryAgentService>(
            client => client.BaseAddress = new Uri("https+http://inventoryservice"));

        builder.Services.AddHttpClient<MatchmakingAgentService>(
            client => client.BaseAddress = new Uri("https+http://matchmakingservice"));

        builder.Services.AddHttpClient<LocationAgentService>(
            client => client.BaseAddress = new Uri("https+http://locationservice"));

        builder.Services.AddHttpClient<NavigationAgentService>(
            client => client.BaseAddress = new Uri("https+http://navigationservice"));
    }
}

/// <summary>
/// Registers orchestration services for different multi-agent patterns (LLM mode).
/// </summary>
static void RegisterOrchestrationServices(WebApplicationBuilder builder)
{
    builder.Services.AddScoped<SequentialOrchestrationService>();
    builder.Services.AddScoped<ConcurrentOrchestrationService>();
    builder.Services.AddScoped<HandoffOrchestrationService>();
    builder.Services.AddScoped<GroupChatOrchestrationService>();
    builder.Services.AddScoped<MagenticOrchestrationService>();
}
