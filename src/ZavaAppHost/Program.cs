#pragma warning disable CS8604

using Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

// ============================================================================
// SECTION 1: INFRASTRUCTURE RESOURCES
// ============================================================================

// SQLite database configuration
// The database file will be stored in the DataService project directory
var productsDb = builder.AddSqlite("productsDb");

// Microsoft Foundry project connection - used for agent services
IResourceBuilder<IResourceWithConnectionString>? microsoftfoundryproject;
var chatDeploymentName = "gpt-5-mini";
var embeddingsDeploymentName = "text-embedding-3-small";

// TenantId - used for agent services
IResourceBuilder<IResourceWithConnectionString>? tenantId;

// Application Insights for telemetry
IResourceBuilder<IResourceWithConnectionString>? appInsights;

// ============================================================================
// SECTION 2: CORE SERVICES
// ============================================================================

// Products service with database dependency
var dataservice = builder.AddProject<Projects.DataService>("dataservice")
    .WithReference(productsDb)
    .WaitFor(productsDb)
    .WithExternalHttpEndpoints();

// ============================================================================
// SECTION 3: AGENT MICROSERVICES
// ============================================================================

// Individual agent services - each handles a specific agent functionality
var analyzePhotoService = builder.AddProject<Projects.AnalyzePhotoService>("analyzephotoservice")
    .WaitFor(dataservice).WithReference(dataservice)
    .WithExternalHttpEndpoints();

var customerInformationService = builder.AddProject<Projects.CustomerInformationService>("customerinformationservice")
    .WaitFor(dataservice).WithReference(dataservice)
    .WithExternalHttpEndpoints();

var toolReasoningService = builder.AddProject<Projects.ToolReasoningService>("toolreasoningservice")
    .WaitFor(dataservice).WithReference(dataservice)
    .WithExternalHttpEndpoints();

var inventoryService = builder.AddProject<Projects.InventoryService>("inventoryservice")
    .WaitFor(dataservice).WithReference(dataservice)
    .WithExternalHttpEndpoints();

var matchmakingService = builder.AddProject<Projects.MatchmakingService>("matchmakingservice")
    .WaitFor(dataservice).WithReference(dataservice)
    .WithExternalHttpEndpoints();

var locationService = builder.AddProject<Projects.LocationService>("locationservice")
    .WaitFor(dataservice).WithReference(dataservice)
    .WithExternalHttpEndpoints();

var navigationService = builder.AddProject<Projects.NavigationService>("navigationservice")
    .WaitFor(dataservice).WithReference(dataservice)
    .WithExternalHttpEndpoints();

var productSearchService = builder.AddProject<Projects.ProductSearchService>("productsearchservice")
    .WaitFor(dataservice).WithReference(dataservice)
    .WithExternalHttpEndpoints();

// ============================================================================
// SECTION 4: DEMO SERVICES
// ============================================================================

// Single Agent Demo - demonstrates single agent scenarios
var singleAgentDemo = builder.AddProject<Projects.SingleAgentDemo>("singleagentdemo")
    .WaitFor(dataservice).WithReference(dataservice)
    .WaitFor(analyzePhotoService).WithReference(analyzePhotoService)
    .WaitFor(customerInformationService).WithReference(customerInformationService)
    .WaitFor(toolReasoningService).WithReference(toolReasoningService)
    .WaitFor(inventoryService).WithReference(inventoryService)
    .WaitFor(productSearchService).WithReference(productSearchService)
    .WaitFor(matchmakingService).WithReference(matchmakingService)
    .WaitFor(locationService).WithReference(locationService)
    .WaitFor(navigationService).WithReference(navigationService)
    .WithExternalHttpEndpoints();

// Multi Agent Demo - demonstrates multi-agent orchestration
var multiAgentDemo = builder.AddProject<Projects.MultiAgentDemo>("multiagentdemo")
    .WaitFor(dataservice).WithReference(dataservice)
    .WaitFor(analyzePhotoService).WithReference(analyzePhotoService)
    .WaitFor(customerInformationService).WithReference(customerInformationService)
    .WaitFor(toolReasoningService).WithReference(toolReasoningService)
    .WaitFor(inventoryService).WithReference(inventoryService)
    .WaitFor(productSearchService).WithReference(productSearchService)
    .WaitFor(matchmakingService).WithReference(matchmakingService)
    .WaitFor(locationService).WithReference(locationService)
    .WaitFor(navigationService).WithReference(navigationService)
    .WithExternalHttpEndpoints();

// ============================================================================
// SECTION 5: STORE SERVICES
// ============================================================================

// Store - main frontend application
var store = builder.AddProject<Projects.Store>("store")
    .WaitFor(dataservice).WithReference(dataservice)

    .WaitFor(analyzePhotoService).WithReference(analyzePhotoService)
    .WaitFor(customerInformationService).WithReference(customerInformationService)
    .WaitFor(toolReasoningService).WithReference(toolReasoningService)
    .WaitFor(inventoryService).WithReference(inventoryService)
    .WaitFor(matchmakingService).WithReference(matchmakingService)
    .WaitFor(locationService).WithReference(locationService)
    .WaitFor(navigationService).WithReference(navigationService)
    .WaitFor(productSearchService).WithReference(productSearchService)
    
    .WaitFor(singleAgentDemo).WithReference(singleAgentDemo)
    .WaitFor(multiAgentDemo).WithReference(multiAgentDemo)

    //.WithEnvironment("singleAgentDemoDevUIUrl", singleAgentDemoEndpointUrl)

    .WithExternalHttpEndpoints();


// ============================================================================
// SECTION 6: ENVIRONMENT-SPECIFIC CONFIGURATION
// ============================================================================

if (builder.ExecutionContext.IsPublishMode)
{
    // PRODUCTION: Use Azure-provisioned services
    appInsights = builder.AddAzureApplicationInsights("appInsights");
}
else
{
    // DEVELOPMENT: Use connection strings from configuration
    appInsights = builder.AddConnectionString("appinsights", "APPLICATIONINSIGHTS_CONNECTION_STRING");
}

// ============================================================================
// SECTION 7: APPLICATION INSIGHTS CONFIGURATION
// ============================================================================

// Add Application Insights to all services
dataservice.WithReference(appInsights);
analyzePhotoService.WithReference(appInsights);
customerInformationService.WithReference(appInsights);
toolReasoningService.WithReference(appInsights);
inventoryService.WithReference(appInsights);
matchmakingService.WithReference(appInsights);
locationService.WithReference(appInsights);
navigationService.WithReference(appInsights);
productSearchService.WithReference(appInsights);
singleAgentDemo.WithReference(appInsights);
multiAgentDemo.WithReference(appInsights);
store.WithReference(appInsights);

// ============================================================================
// SECTION 8: MICROSOFT FOUNDRY CONFIGURATION
// ============================================================================

// Configure Microsoft Foundry project connection for all agent services
microsoftfoundryproject = builder.AddConnectionString("microsoftfoundryproject");

// Configure Azure Tenant Id for all agent services
tenantId = builder.AddConnectionString("tenantId");

// Add AI configuration to Products service
dataservice
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

// Add Microsoft Foundry configuration to all agent services
analyzePhotoService
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

customerInformationService
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

toolReasoningService
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

inventoryService
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

matchmakingService
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

locationService
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

navigationService
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

productSearchService
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

singleAgentDemo
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

multiAgentDemo
    .WithReference(microsoftfoundryproject)
    .WithReference(tenantId)
    .WithEnvironment("AI_ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AI_embeddingsDeploymentName", embeddingsDeploymentName);

// ============================================================================
// RUN THE APPLICATION
// ============================================================================

builder.Build().Run();