using System.Reflection;

namespace ForgeTrust.Runnable.Web.RazorDocs;

/// <summary>
/// Resolves stylesheet paths for RazorDocs hosts.
/// </summary>
/// <remarks>
/// When the current application's root module lives in the RazorDocs assembly, RazorDocs static web assets are
/// rooted at the application base path, so the generated stylesheet is served from <c>~/css/site.gen.css</c>.
/// When RazorDocs is consumed from another host assembly, the same stylesheet is served from the Razor Class
/// Library asset path under <c>~/_content/ForgeTrust.Runnable.Web.RazorDocs/css/site.gen.css</c>.
/// </remarks>
internal sealed class RazorDocsAssetPathResolver
{
    internal const string RootStylesheetPath = "~/css/site.gen.css";
    internal const string PackagedStylesheetPath = "~/_content/ForgeTrust.Runnable.Web.RazorDocs/css/site.gen.css";

    private static readonly Assembly RazorDocsAssembly = typeof(RazorDocsWebModule).Assembly;

    private RazorDocsAssetPathResolver(string stylesheetPath)
    {
        StylesheetPath = stylesheetPath;
    }

    /// <summary>
    /// Gets the application-relative stylesheet path to use from RazorDocs layouts.
    /// </summary>
    public string StylesheetPath { get; }

    /// <summary>
    /// Creates the default asset-path resolver used when only the RazorDocs services are registered.
    /// </summary>
    /// <returns>A resolver that assumes RazorDocs is embedded in another host.</returns>
    internal static RazorDocsAssetPathResolver CreateDefault()
    {
        return new RazorDocsAssetPathResolver(PackagedStylesheetPath);
    }

    /// <summary>
    /// Creates the asset-path resolver for the supplied root module assembly.
    /// </summary>
    /// <param name="rootModuleAssembly">The assembly that owns the current host's root module.</param>
    /// <returns>The resolver that matches the current host's RazorDocs asset layout.</returns>
    internal static RazorDocsAssetPathResolver CreateForRootModule(Assembly rootModuleAssembly)
    {
        return new RazorDocsAssetPathResolver(
            rootModuleAssembly == RazorDocsAssembly
                ? RootStylesheetPath
                : PackagedStylesheetPath);
    }
}
