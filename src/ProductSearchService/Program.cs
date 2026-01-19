using ZavaMAFFoundry;
using ZavaMAFLocal;
using ZavaMAFOllama;
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

// Register MAF Local agents (locally created with IChatClient)
builder.AddMAFLocalAgents();

// Register MAF Ollama agents (Ollama-based agents)
builder.AddMAFOllamaAgents();

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