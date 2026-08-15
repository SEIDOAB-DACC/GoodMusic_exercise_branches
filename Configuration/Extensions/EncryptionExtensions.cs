using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Configuration.Options;

namespace Configuration.Extensions;

public static class EncryptionExtensions
{
    public static IServiceCollection AddEncryptions(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.Configure<AesEncryptionOptions>(
            options => configuration.GetSection(AesEncryptionOptions.Position).Bind(options));
        serviceCollection.AddTransient<Encryptions>();

        return serviceCollection;
    }
}