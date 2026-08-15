using Microsoft.Extensions.DependencyInjection;
using Configuration.Options;

namespace Configuration.Extensions;

public static class VersionExtensions
{
    public static IServiceCollection AddVersionInfo(this IServiceCollection serviceCollection)
    {
        serviceCollection.Configure<VersionOptions>(options => VersionOptions.ReadFromAssembly(options));

        return serviceCollection;
    }
}
