using ZavaMAFFoundry;
using ZavaMAFLocal;
using DataServiceClient;
using NavigationService.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();

// Add DataServiceClient for accessing DataService endpoints
builder.Services.AddDataServiceClient("https+http://dataservice");

// Register MAF Foundry agents (Microsoft Foundry)
builder.AddMAFFoundryAgents();

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
app.MapNavigationEndpoints();

app.Run();
