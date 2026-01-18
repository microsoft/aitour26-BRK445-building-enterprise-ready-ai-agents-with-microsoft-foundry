using Microsoft.Agents.AI.DevUI;
using SingleAgentDemo.Services;
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
builder.Services.AddDataServiceClient("https+http://dataservice");

// Add DataServiceClient for accessing DataService endpoints
builder.Services.AddDataServiceClient("https+http://dataservice", builder.Environment.IsDevelopment());

// Register MAF Foundry agents (Microsoft Foundry)
builder.AddMAFFoundryAgents();

// Register MAF Local agents (locally created with IChatClient)
builder.AddMAFLocalAgents();

// Register HTTP clients for external services (used by LLM direct call and DirectCall modes)
RegisterHttpClients(builder);

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
    logger.LogInformation("SingleAgentDemo health/log requested");
    var appInsights = config["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? config["appinsights"] ?? "<not-set>";
    var foundryCnn = config.GetConnectionString("microsoftfoundrycnnstring") ?? "<not-set>";
    var foundryProject = config.GetConnectionString("microsoftfoundryproject") ?? "<not-set>";
    var env = config["ASPNETCORE_ENVIRONMENT"] ?? "<unknown>";

    logger.LogInformation("SingleAgentDemo Config - Env: {Env}, AppInsights: {AppInsights}, FoundryCnn: {FoundryCnn}, FoundryProject: {FoundryProject}", env, appInsights, foundryCnn, foundryProject);

    return Results.Ok(new
    {
        service = "singleagentdemo",
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
    // Register service dependencies as scoped
    builder.Services.AddScoped<ToolReasoningService>();
    builder.Services.AddScoped<InventoryService>();
    builder.Services.AddScoped<AnalyzePhotoService>();
    builder.Services.AddScoped<CustomerInformationService>();
    builder.Services.AddScoped<ProductSearchService>();

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddHttpClient<AnalyzePhotoService>(
            client => client.BaseAddress = new Uri("https+http://analyzephotoservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        builder.Services.AddHttpClient<CustomerInformationService>(
            client => client.BaseAddress = new Uri("https+http://customerinformationservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        builder.Services.AddHttpClient<ToolReasoningService>(
            client => client.BaseAddress = new Uri("https+http://toolreasoningservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        builder.Services.AddHttpClient<InventoryService>(
            client => client.BaseAddress = new Uri("https+http://inventoryservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        builder.Services.AddHttpClient<ProductSearchService>(
            client => client.BaseAddress = new Uri("https+http://productsearchservice"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
    }
    else
    {
        builder.Services.AddHttpClient<AnalyzePhotoService>(
            client => client.BaseAddress = new Uri("https+http://analyzephotoservice"));

        builder.Services.AddHttpClient<CustomerInformationService>(
            client => client.BaseAddress = new Uri("https+http://customerinformationservice"));

        builder.Services.AddHttpClient<ToolReasoningService>(
            client => client.BaseAddress = new Uri("https+http://toolreasoningservice"));

        builder.Services.AddHttpClient<InventoryService>(
            client => client.BaseAddress = new Uri("https+http://inventoryservice"));

        builder.Services.AddHttpClient<ProductSearchService>(
            client => client.BaseAddress = new Uri("https+http://productsearchservice"));
    }
}