using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RazorDocsOptionsTests
{
    [Fact]
    public void AddRazorDocs_ShouldFallbackToLegacyRepositoryRootSetting()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RepositoryRoot"] = "/tmp/repo-root"
                    })
                .Build());

        services.AddRazorDocs();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value;

        Assert.Equal("/tmp/repo-root", options.Source.RepositoryRoot);
    }

    [Fact]
    public void AddRazorDocs_ShouldTrimAndDeduplicateConfiguredNamespacePrefixes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RazorDocs:Sidebar:NamespacePrefixes:0"] = " ForgeTrust.Runnable. ",
                        ["RazorDocs:Sidebar:NamespacePrefixes:1"] = "ForgeTrust.Runnable.",
                        ["RazorDocs:Sidebar:NamespacePrefixes:2"] = "  "
                    })
                .Build());

        services.AddRazorDocs();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value;

        Assert.Equal(["ForgeTrust.Runnable."], options.Sidebar.NamespacePrefixes);
    }

    [Fact]
    public void Validator_ShouldRejectBundleModeBeforeSliceTwo()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Mode = RazorDocsMode.Bundle,
            Bundle = new RazorDocsBundleOptions { Path = "/tmp/docs.bundle.json" }
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("not implemented", StringComparison.OrdinalIgnoreCase));
    }
}
