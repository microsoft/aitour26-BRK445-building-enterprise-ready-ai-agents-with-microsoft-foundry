//using SharedEntities;
//using SearchEntities;

//namespace DataServiceClient;

///// <summary>
///// Interface for interacting with DataService endpoints
///// </summary>
//public interface IDataServiceClient
//{
//    // Product endpoints
//    Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default);
//    Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
//    Task<SearchResponse?> SearchProductsAsync(string searchTerm, CancellationToken cancellationToken = default);
//    Task<SearchResponse?> AISearchProductsAsync(string searchTerm, CancellationToken cancellationToken = default);
    
//    // Customer endpoints
//    Task<List<CustomerInformation>> GetCustomersAsync(CancellationToken cancellationToken = default);
//    Task<CustomerInformation?> GetCustomerByIdAsync(string id, CancellationToken cancellationToken = default);
    
//    // Tool endpoints
//    Task<List<ToolRecommendation>> GetToolsAsync(CancellationToken cancellationToken = default);
//    Task<ToolRecommendation?> GetToolBySkuAsync(string sku, CancellationToken cancellationToken = default);
//    Task<List<ToolRecommendation>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);
    
//    // Location endpoints
//    Task<List<StoreLocation>> GetLocationsAsync(CancellationToken cancellationToken = default);
//    Task<List<StoreLocation>> SearchLocationsAsync(string? query = null, CancellationToken cancellationToken = default);
//}
