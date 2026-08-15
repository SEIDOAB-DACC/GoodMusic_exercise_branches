using System.Reflection;
using System.Text.RegularExpressions;

namespace Configuration.Options;

/// <summary>
/// Represents version and build information about the application
/// </summary>
public class VersionOptions
{
    public string AppEnvironment { get; set; }
    public string AssemblyVersion { get; set; }
    public string FileVersion { get; set; }
    public string InformationalVersion { get; set; }
    public string GitCommitHash { get; set; }


    public string BuildTime { get; set; }
    public string BuildMachine { get; set; }
    public string BuildUser { get; set; }

    public string Company { get; set; }
    public string Product { get; set; }
    public string Description { get; set; }
    public string Copyright { get; set; }
    public string CompanyUrl { get; set; }

    /// <summary>
    /// Creates a VersionInfo object from the current assembly
    /// </summary>
    /// <returns>A populated VersionInfo object</returns>
    public static VersionOptions ReadFromAssembly(VersionOptions options)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyName = assembly.GetName();

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
        var gitCommitHash = ExtractGitCommitHash(informationalVersion);

        options.AppEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        options.AssemblyVersion = assemblyName.Version?.ToString() ?? "Unknown";
        options.FileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";
        options.InformationalVersion = informationalVersion;
        options.GitCommitHash = gitCommitHash;

        options.BuildTime = GetAssemblyMetadata(assembly, "BuildTime") ?? "Unknown";
        options.BuildMachine = GetAssemblyMetadata(assembly, "BuildMachine") ?? "Unknown";
        options.BuildUser = GetAssemblyMetadata(assembly, "BuildUser") ?? "Unknown";

        options.Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Unknown";
        options.Product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Unknown";
        options.Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "Unknown";
        options.Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? $"Copyright © Unknown {DateTime.UtcNow.Year}";
        options.CompanyUrl = GetAssemblyMetadata(assembly, "CompanyUrl") ?? "Unknown";

        return options;
    }
    private static string GetAssemblyMetadata(Assembly assembly, string key)
    {
        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                      .FirstOrDefault(attr => attr.Key == key)?.Value;
    }

    private static string ExtractGitCommitHash(string informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion))
            return "Unknown";

        // Use regex to extract git commit hash after '+' character
        // Pattern matches '+' followed by alphanumeric characters (git hash)
        var match = Regex.Match(informationalVersion, @"\+([a-fA-F0-9]+)");
        if (match.Success)
        {
            var hashPart = match.Groups[1].Value;
            // Take first 7-10 characters as short hash, or full if shorter
            return hashPart.Length > 10 ? hashPart.Substring(0, 10) : hashPart;
        }

        return "Unknown";
    }
}
