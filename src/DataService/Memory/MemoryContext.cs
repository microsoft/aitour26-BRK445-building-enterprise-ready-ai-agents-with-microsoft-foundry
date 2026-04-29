using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Newtonsoft.Json;
using SearchEntities;
using SharedEntities;
using System.Text;
using VectorEntities;
using ZavaDatabaseInitialization;
using ZavaWorkingModes;

namespace DataService.Memory;

public class MemoryContext
{
    private ILogger _logger;
    private IChatClient? _chatClient;
    private IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private readonly IChatClient? _ollamaChatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _ollamaEmbeddingGenerator;
    private readonly IChatClient? _defaultChatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _defaultEmbeddingGenerator;

    private VectorStoreCollection<int, ProductVector>? _productsCollection;
    private string _systemPrompt = "";
    private bool _isMemoryCollectionInitialized = false;

    public MemoryContext(
        ILogger logger,
        IChatClient? chatClient,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator,
        [FromKeyedServices("ollama")] IChatClient ollamaChatClient,
        [FromKeyedServices("ollama")] IEmbeddingGenerator<string, Embedding<float>>? ollamaEmbeddingGenerator)
    {
        _logger = logger;
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;

        _defaultChatClient = chatClient;
        _defaultEmbeddingGenerator = embeddingGenerator;

        _ollamaChatClient = ollamaChatClient;
        _ollamaEmbeddingGenerator = ollamaEmbeddingGenerator;

        _logger.LogInformation("Memory context created");
    }

    public async Task<bool> InitMemoryContextAsync(Context db)
    {
        _logger.LogInformation("Initializing memory context");
        var vectorProductStore = new InMemoryVectorStore();
        _productsCollection = vectorProductStore.GetCollection<int, ProductVector>("products");
        await _productsCollection.EnsureCollectionExistsAsync();

        // define system prompt
        _systemPrompt = "You are a useful assistant. You always reply with a short and funny message. If you do not know an answer, you say 'I don't know that.' You only answer questions related to home improvement products. For any other type of questions, explain to the user that you only answer home improvement products questions. Do not store memory of the chat conversation.";

        _logger.LogInformation("Get a copy of the list of products");
        // get a copy of the list of products
        var products = await db.Product.ToListAsync();

        _logger.LogInformation("Filling products in memory");

        // iterate over the products and add them to the memory
        foreach (var product in products)
        {
            try
            {
                _logger.LogInformation("Adding product to memory: {Product}", product.Name);
                var productInfo = $"[{product.Name}] is a product that costs [{product.Price}] and is described as [{product.Description}]";

                // new product vector
                var productVector = new ProductVector
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    ImageUrl = product.ImageUrl
                };
                var result = await _embeddingGenerator.GenerateVectorAsync(productInfo);
                productVector.Vector = result.ToArray();

                await _productsCollection.UpsertAsync(productVector);
                _logger.LogInformation("Product added to memory: {Product}", product.Name);
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Error adding product to memory");
            }
        }

        _isMemoryCollectionInitialized = true;
        _logger.LogInformation("DONE! Filling products in memory");
        return true;
    }

    public virtual async Task<SearchResponse> Search(
        string search, 
        Context db, 
        WorkingMode workingMode = WorkingMode.MafLocal)
    {
        if(workingMode == WorkingMode.MafOllama)
        {
            _logger.LogInformation("Using Ollama chat client and embedding generator");
            _chatClient = _ollamaChatClient;
            _embeddingGenerator = _ollamaEmbeddingGenerator;
        }
        else
        {
            _logger.LogInformation("Using default chat client and embedding generator");
            _chatClient = _defaultChatClient;
            _embeddingGenerator = _defaultEmbeddingGenerator;
        }

        if (!_isMemoryCollectionInitialized)
        {
            await InitMemoryContextAsync(db);
        }

        var response = new SearchResponse
        {
            Response = $"I don't know the answer for your question. Your question is: [{search}]"
        };

        try
        {
            var result = await _embeddingGenerator.GenerateVectorAsync(search);
            var vectorSearchQuery = result.ToArray();

            // search the vector database for the most similar product        
            var sbFoundProducts = new StringBuilder();
            int productPosition = 1;

            await foreach (var resultItem in _productsCollection.SearchAsync(vectorSearchQuery, top: 3))
            {
                if (resultItem.Score > 0.4)
                {
                    var product = await db.FindAsync<Product>(resultItem.Record.Id);
                    if (product != null)
                    {
                        response.Products.Add(product);
                        sbFoundProducts.AppendLine($"- Product {productPosition}:");
                        sbFoundProducts.AppendLine($"  - Name: {product.Name}");
                        sbFoundProducts.AppendLine($"  - Description: {product.Description}");
                        sbFoundProducts.AppendLine($"  - Price: {product.Price}");
                        productPosition++;
                    }
                }
            }

            // let's improve the response message
            var prompt = @$"You are an intelligent assistant helping clients with their search about outdoor products. 
Generate a catchy and friendly message using the information below.
Add a comparison between the products found and the search criteria.
Include products details.
Format your response in clear, well-structured markdown with:
- Use headers (##) for sections
- Use bullet points for product lists
- Use **bold** for important information
- Use > for important notes
    - User Question: {search}
    - Found Products: 
{sbFoundProducts}";

            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(ChatRole.System, _systemPrompt),
                new(ChatRole.System, prompt)
            };

            _logger.LogInformation("{ChatHistory}", JsonConvert.SerializeObject(messages));

            var resultPrompt = await _chatClient.GetResponseAsync(messages);
            response.Response = resultPrompt.Text!;

        }
        catch (Exception ex)
        {
            response.Response = $"An error occurred: {ex.Message}";
            _logger.LogError(ex, "Error during search");
        }
        return response;
    }
}