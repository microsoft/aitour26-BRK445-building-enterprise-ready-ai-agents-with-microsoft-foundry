using ZavaMAFFoundry;
using ZavaMAFLocal;
using DataServiceClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add DataServiceClient for accessing DataService endpoints
builder.Services.AddDataServiceClient("https+http://dataservice");

// Register MAF Foundry agents (Microsoft Foundry)
builder.AddMAFFoundryAgents();

var microsoftFoundryCnnString = builder.Configuration.GetConnectionString("microsoftfoundrycnnstring");
var chatDeploymentName = builder.Configuration["AI_ChatDeploymentName"] ?? "gpt-5-mini";

builder.AddAzureOpenAIClient(connectionName: "microsoftfoundrycnnstring",
    configureSettings: settings =>
    {
        if (string.IsNullOrEmpty(settings.Key))
        {
            settings.Credential = new Azure.Identity.DefaultAzureCredential();
        }
    }).AddChatClient(chatDeploymentName);

// Register MAF Local agents (locally created with IChatClient)
builder.AddMAFLocalAgents();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
