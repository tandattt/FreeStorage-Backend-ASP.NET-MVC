using Hangfire;
using Hangfire.MySql;
using Microsoft.Extensions.DependencyInjection;

namespace ImageUploadApp.Configurations;

public static class HangfireConfiguration
{
    public static IServiceCollection AddHangfireWithMySql(this IServiceCollection services, string connectionString)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(connectionString, new MySqlStorageOptions
            {
                PrepareSchemaIfNecessary = true,
                TransactionIsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
            })));

        services.AddHangfireServer();
        return services;
    }
}
