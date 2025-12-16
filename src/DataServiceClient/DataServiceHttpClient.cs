using SharedEntities;
using SearchEntities;
using System.Net.Http.Json;

namespace DataServiceClient;

/// <summary>
/// HTTP client implementation for DataService endpoints
/// </summary>
public class DataServiceClient 
{
    private readonly HttpClient _httpClient;

    public DataServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    #region Product Methods

    public async Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/product", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Product>>(cancellationToken) ?? new List<Product>();
        }
        catch (Exception)
        {
            return new List<Product>();
        }
    }

    public async Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/product/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            
            return await response.Content.ReadFromJsonAsync<Product>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<SearchResponse?> SearchProductsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/product/search/{searchTerm}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            
            return await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<SearchResponse?> AISearchProductsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/aisearch/{searchTerm}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            
            return await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Customer Methods

    public async Task<List<CustomerInformation>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/customer", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<CustomerInformation>>(cancellationToken) ?? new List<CustomerInformation>();
        }
        catch (Exception)
        {
            return new List<CustomerInformation>();
        }
    }

    public async Task<CustomerInformation?> GetCustomerByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/customer/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            
            return await response.Content.ReadFromJsonAsync<CustomerInformation>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Tool Methods

    public async Task<List<ToolRecommendation>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tool", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ToolRecommendation>>(cancellationToken) ?? new List<ToolRecommendation>();
        }
        catch (Exception)
        {
            return new List<ToolRecommendation>();
        }
    }

    public async Task<ToolRecommendation?> GetToolBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/tool/{sku}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            
            return await response.Content.ReadFromJsonAsync<ToolRecommendation>(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<List<ToolRecommendation>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tool/available", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ToolRecommendation>>(cancellationToken) ?? new List<ToolRecommendation>();
        }
        catch (Exception)
        {
            return new List<ToolRecommendation>();
        }
    }

    #endregion

    #region Location Methods

    public async Task<List<StoreLocation>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/location", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<StoreLocation>>(cancellationToken) ?? new List<StoreLocation>();
        }
        catch (Exception)
        {
            return new List<StoreLocation>();
        }
    }

    public async Task<List<StoreLocation>> SearchLocationsAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(query) ? "/api/location" : $"/api/location/search?query={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<StoreLocation>>(cancellationToken) ?? new List<StoreLocation>();
        }
        catch (Exception)
        {
            return new List<StoreLocation>();
        }
    }

    #endregion
}
