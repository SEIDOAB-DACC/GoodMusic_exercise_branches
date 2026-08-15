using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Configuration.Extensions;

public static class SecretsExtensions
{
    const string _appsettingfile = "appsettings.json";

    //to use either user secrets or azure key vault depending on UseAzureKeyVault tag in appsettings.json
    //Azure key vault access parameters location are set in <AzureProjectSettings> tag in the csproj file
    //User secret id is set in <UserSecretsId>
    public static IConfigurationBuilder AddSecrets(this IConfigurationBuilder config, IHostEnvironment environment, string appsettingsFolder=null)
    {
        // current directory is either the application or the dbContext when running migrations
        appsettingsFolder ??= Directory.GetCurrentDirectory();
#if DEBUG
        config.SetBasePath(appsettingsFolder)
                .AddJsonFile(_appsettingfile, optional: true, reloadOnChange: true);
#else   
        //In production the appsettings.json is always in the current directory     
        config.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(_appsettingfile, optional: true, reloadOnChange: true);
#endif
        //assume production unless we are in development
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

        // Build a temporary configuration to read the SecretStorage setting from appsettings.json
        var tempConfig = config.Build();
        string secretStorage = tempConfig.GetValue<string>("ApplicationSecrets:SecretStorage");
        Console.WriteLine($"Using Secret Storage: {secretStorage}");

        //to use either user secrets or azure key vault depending on SecretStorage tag in appsettings.json
        if (environment.IsDevelopment())
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            //In development mode user secrets are always used, even to read the Azure Key Vault access parameters
            // Load user secrets from Configuration project assembly
            var assembly = System.Reflection.Assembly.Load("Configuration");
            config.AddUserSecrets(assembly);

            // Build a temporary configuration to read from appsettings.json and user secrets
            tempConfig = config.Build();
        
            // Read the UserSecretsId programmatically in order to display it during migration runs
            var userSecretsIdAttribute = assembly.GetCustomAttributes(typeof(UserSecretsIdAttribute), false)
                .FirstOrDefault() as UserSecretsIdAttribute;
            var userSecretsId = userSecretsIdAttribute?.UserSecretsId;
            Console.WriteLine($"User Secrets ID: {userSecretsId}");

            if (secretStorage == "UserSecrets")
            {
                // In development, but only use user secrets
                Console.WriteLine("Using User Secrets in Development environment.");

            }
            else
            {
                throw new InvalidOperationException("Invalid SecretStorage value. Use 'UserSecrets' or 'AzureKeyVault'.");
            }
        }
        else
        {
            throw new InvalidOperationException("Invalid SecretStorage value. 'AzureKeyVault' for production is not implemented.");
        }

        return config;
    }
}

