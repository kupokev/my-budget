using Microsoft.Extensions.DependencyInjection;
using MyBudget.Contracts;
using MyBudget.UI.Services;

namespace MyBudget.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the UI's services. Hosts (Photino now, MAUI Android later) call this and nothing else.</summary>
    public static IServiceCollection AddMyBudgetUI(this IServiceCollection services, ApiClientOptions options)
    {
        services.AddSingleton(options);
        services.AddHttpClient<ApiClient>(client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add(ApiKeyHeader.Name, options.ApiKey);
        });
        return services;
    }
}
