using Microsoft.Agents.AI.DevUI;
using Store.Components;
using Store.Services;
using DataServiceClient;

var builder = WebApplication.CreateBuilder(args);

// add aspire service defaults
builder.AddServiceDefaults();

builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<AgentFrameworkService>();

// Add DataServiceClient for accessing DataService endpoints
builder.Services.AddDataServiceClient("https+http://dataservice", builder.Environment.IsDevelopment());

builder.Services.AddHttpClient<SingleAgentService>(
    static client => client.BaseAddress = new("https+http://singleagentdemo"));

// Add named HttpClient for SingleAgentService for redirect endpoint
builder.Services.AddHttpClient("SingleAgentService",
    static client => client.BaseAddress = new("https+http://singleagentdemo"));

builder.Services.AddHttpClient<MultiAgentService>(
    static client => client.BaseAddress = new("https+http://multiagentdemo"));

// Add named HttpClient for MultiAgentService for redirect endpoint
builder.Services.AddHttpClient("MultiAgentService",
    static client => client.BaseAddress = new("https+http://multiagentdemo"));

// Register services for OpenAI responses and conversations (required for DevUI)
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

// Add DevUI for agent debugging and visualization
builder.AddDevUI();

// blazor bootstrap
builder.Services.AddBlazorBootstrap();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// aspire map default endpoints
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// Map DevUI endpoints for agent debugging (development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenAIResponses();
    app.MapOpenAIConversations();
    app.MapDevUI();
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// API endpoints to resolve and redirect to service DevUI pages
app.MapGet("/api/devui/singleagent", async (HttpContext context, IHttpClientFactory clientFactory, ILogger<Program> logger) =>
{
    var client = clientFactory.CreateClient("SingleAgentService");
    var baseUrl = client.BaseAddress?.ToString().TrimEnd('/');
    
    if (string.IsNullOrEmpty(baseUrl))
    {
        logger.LogWarning("SingleAgentDemo service URL not available");
        return Results.NotFound("SingleAgentDemo service URL not available");
    }
    
    // Try to make a HEAD request to check if the service is available
    try
    {
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/"));
        var resolvedUrl = response.RequestMessage?.RequestUri?.ToString().TrimEnd('/');
        if (!string.IsNullOrEmpty(resolvedUrl))
        {
            logger.LogInformation("Redirecting to SingleAgentDemo DevUI: {Url}", $"{resolvedUrl}/devui");
            return Results.Redirect($"{resolvedUrl}/devui");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to resolve SingleAgentDemo URL via HEAD request, using base URL");
    }
    
    logger.LogInformation("Redirecting to SingleAgentDemo DevUI: {Url}", $"{baseUrl}/devui");
    return Results.Redirect($"{baseUrl}/devui");
});

app.MapGet("/api/devui/multiagent", async (HttpContext context, IHttpClientFactory clientFactory, ILogger<Program> logger) =>
{
    var client = clientFactory.CreateClient("MultiAgentService");
    var baseUrl = client.BaseAddress?.ToString().TrimEnd('/');
    
    if (string.IsNullOrEmpty(baseUrl))
    {
        logger.LogWarning("MultiAgentDemo service URL not available");
        return Results.NotFound("MultiAgentDemo service URL not available");
    }
    
    // Try to make a HEAD request to check if the service is available
    try
    {
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/"));
        var resolvedUrl = response.RequestMessage?.RequestUri?.ToString().TrimEnd('/');
        if (!string.IsNullOrEmpty(resolvedUrl))
        {
            logger.LogInformation("Redirecting to MultiAgentDemo DevUI: {Url}", $"{resolvedUrl}/devui");
            return Results.Redirect($"{resolvedUrl}/devui");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to resolve MultiAgentDemo URL via HEAD request, using base URL");
    }
    
    logger.LogInformation("Redirecting to MultiAgentDemo DevUI: {Url}", $"{baseUrl}/devui");
    return Results.Redirect($"{baseUrl}/devui");
});

app.Run();
