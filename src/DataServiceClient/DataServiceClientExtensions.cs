using Microsoft.Extensions.DependencyInjection;

namespace DataServiceClient;

/// <summary>
/// Extension methods for registering DataServiceClient
/// </summary>
public static class DataServiceClientExtensions
{
    /// <summary>
    /// Adds the DataServiceClient to the service collection with the specified base address
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="baseAddress">The base address for the DataService API</param>
    /// <param name="isDevelopment">Whether the application is running in development mode</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDataServiceClient(this IServiceCollection services, string baseAddress, bool isDevelopment = false)
    {
        services.AddScoped<DataServiceClient>();
        
        var httpClientBuilder = services.AddHttpClient<DataServiceClient>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        });

        if (isDevelopment)
        {
            httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        return services;
    }
}
