using SharedEntities;
using ZavaAgentsMetadata;

namespace SingleAgentDemo.Services;

public class AnalyzePhotoService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnalyzePhotoService> _logger;
    private string _framework = AgentMetadata.FrameworkIdentifiers.MafLocal; // Default to Microsoft Agent Framework

    public AnalyzePhotoService(HttpClient httpClient, ILogger<AnalyzePhotoService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sets the agent framework to use for service calls
    /// </summary>
    /// <param name="framework">"llm" for LLM Direct Call, or "maf" for Microsoft Agent Framework</param>
    public void SetFramework(string framework)
    {
        _framework = framework?.ToLowerInvariant() ?? AgentMetadata.FrameworkIdentifiers.MafLocal;
        _logger.LogInformation($"[AnalyzePhotoService] Framework set to: {_framework}");
    }

    public async Task<PhotoAnalysisResult> AnalyzePhotoAsync(IFormFile image, string prompt)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var imageStream = image.OpenReadStream();
            using var streamContent = new StreamContent(imageStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(image.ContentType);
            
            content.Add(streamContent, "image", image.FileName);
            content.Add(new StringContent(prompt), "prompt");

            // Map framework to actual controller endpoints
            var endpoint = _framework switch
            {
                AgentMetadata.FrameworkIdentifiers.Llm => "/api/PhotoAnalysis/analyzellm",
                AgentMetadata.FrameworkIdentifiers.DirectCall => "/api/PhotoAnalysis/analyzedirectcall",
                AgentMetadata.FrameworkIdentifiers.MafFoundry => $"/api/PhotoAnalysis/analyze{AgentMetadata.FrameworkIdentifiers.MafFoundry}",
                _ => $"/api/PhotoAnalysis/analyze{AgentMetadata.FrameworkIdentifiers.MafLocal}"  // Default to MAF_Local
            };
            _logger.LogInformation($"[AnalyzePhotoService] Calling endpoint: {endpoint} (framework: {_framework})");
            var response = await _httpClient.PostAsync(endpoint, content);
            
            _logger.LogInformation($"AnalyzePhotoService HTTP status code: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PhotoAnalysisResult>();
                return result ?? CreateFallbackPhotoAnalysis(prompt);
            }
            
            _logger.LogWarning("AnalyzePhotoService returned non-success status: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AnalyzePhotoService");
        }

        return CreateFallbackPhotoAnalysis(prompt);
    }

    private PhotoAnalysisResult CreateFallbackPhotoAnalysis(string prompt)
    {
        return new PhotoAnalysisResult 
        { 
            Description = $"Room analysis for prompt: {prompt}. Detected painted walls with preparation needed.",
            DetectedMaterials = ["paint", "wall", "surface preparation"]
        };
    }
}