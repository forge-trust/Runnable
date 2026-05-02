using System.Text;
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
    public void AddRazorDocs_ShouldTrimConfiguredBundlePath()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RazorDocs:Bundle:Path"] = " /tmp/docs.bundle.json "
                    })
                .Build());

        services.AddRazorDocs();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value;

        Assert.Equal("/tmp/docs.bundle.json", options.Bundle.Path);
    }

    [Fact]
    public void AddRazorDocs_ShouldTrimConfiguredContributorOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RazorDocs:Contributor:DefaultBranch"] = " main ",
                        ["RazorDocs:Contributor:SourceUrlTemplate"] = " https://example.com/blob/{branch}/{path} ",
                        ["RazorDocs:Contributor:EditUrlTemplate"] = " https://example.com/edit/{branch}/{path} "
                    })
                .Build());

        services.AddRazorDocs();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value;

        Assert.Equal("main", options.Contributor.DefaultBranch);
        Assert.Equal("https://example.com/blob/{branch}/{path}", options.Contributor.SourceUrlTemplate);
        Assert.Equal("https://example.com/edit/{branch}/{path}", options.Contributor.EditUrlTemplate);
    }

    [Fact]
    public void AddRazorDocs_ShouldRejectExplicitWhitespaceSourceRepositoryRoot()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RazorDocs:Source:RepositoryRoot"] = "   ",
                        ["RepositoryRoot"] = "/tmp/legacy-root"
                    })
                .Build());

        services.AddRazorDocs();

        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value);

        Assert.Contains(
            ex.Failures,
            failure => failure.Contains("RepositoryRoot cannot be whitespace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddRazorDocs_ShouldRehydrateNullNestedOptionsObjects()
    {
        var services = new ServiceCollection();
        using var configStream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                """
                {
                  "RazorDocs": {
                    "Source": null,
                    "Bundle": null,
                    "Sidebar": null,
                    "Contributor": null
                  }
                }
                """));
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddJsonStream(configStream)
                .Build());

        services.AddRazorDocs();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value;

        Assert.NotNull(options.Source);
        Assert.NotNull(options.Bundle);
        Assert.NotNull(options.Sidebar);
        Assert.NotNull(options.Contributor);
        Assert.NotNull(options.Sidebar.NamespacePrefixes);
        Assert.Empty(options.Sidebar.NamespacePrefixes);
    }

    [Fact]
    public void AddRazorDocs_ShouldPreserveExistingNestedOptionsObjects()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RepositoryRoot"] = "/tmp/legacy-root"
                    })
                .Build());

        var source = new RazorDocsSourceOptions { RepositoryRoot = " /tmp/configured-root " };
        var bundle = new RazorDocsBundleOptions { Path = " /tmp/docs.bundle.json " };
        var sidebar = new RazorDocsSidebarOptions
        {
            NamespacePrefixes = [" Contoso.Product. ", "contoso.product.", " "]
        };
        var contributor = new RazorDocsContributorOptions
        {
            DefaultBranch = " main ",
            SourceUrlTemplate = " https://example.com/blob/{branch}/{path} ",
            EditUrlTemplate = " https://example.com/edit/{branch}/{path} "
        };

        services.Configure<RazorDocsOptions>(
            options =>
            {
                options.Source = source;
                options.Bundle = bundle;
                options.Sidebar = sidebar;
                options.Contributor = contributor;
            });

        services.AddRazorDocs();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value;

        Assert.Same(source, options.Source);
        Assert.Same(bundle, options.Bundle);
        Assert.Same(sidebar, options.Sidebar);
        Assert.Same(contributor, options.Contributor);
        Assert.Equal("/tmp/configured-root", options.Source.RepositoryRoot);
        Assert.Equal("/tmp/docs.bundle.json", options.Bundle.Path);
        Assert.Equal(["Contoso.Product."], options.Sidebar.NamespacePrefixes);
        Assert.Equal("main", options.Contributor.DefaultBranch);
        Assert.Equal("https://example.com/blob/{branch}/{path}", options.Contributor.SourceUrlTemplate);
        Assert.Equal("https://example.com/edit/{branch}/{path}", options.Contributor.EditUrlTemplate);
    }

    [Fact]
    public void AddRazorDocs_ShouldRehydrateExplicitlyNullNestedOptionsObjects()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddRazorDocs();
        services.Configure<RazorDocsOptions>(
            options =>
            {
                options.Source = null!;
                options.Bundle = null!;
                options.Sidebar = null!;
                options.Contributor = null!;
            });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value;

        Assert.NotNull(options.Source);
        Assert.NotNull(options.Bundle);
        Assert.NotNull(options.Sidebar);
        Assert.NotNull(options.Contributor);
        Assert.NotNull(options.Sidebar.NamespacePrefixes);
        Assert.Empty(options.Sidebar.NamespacePrefixes);
    }

    [Fact]
    public void AddRazorDocs_ShouldRehydrateExplicitlyNullNamespacePrefixes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddRazorDocs();
        services.Configure<RazorDocsOptions>(
            options =>
            {
                options.Sidebar = new RazorDocsSidebarOptions
                {
                    NamespacePrefixes = null!
                };
            });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RazorDocsOptions>>().Value;

        Assert.NotNull(options.Sidebar);
        Assert.NotNull(options.Sidebar.NamespacePrefixes);
        Assert.Empty(options.Sidebar.NamespacePrefixes);
    }

    [Fact]
    public void Validator_ShouldRejectUnsupportedModeValue()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Mode = (RazorDocsMode)999
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Unsupported RazorDocs mode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_ShouldRejectNullNestedOptionObjects()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Source = null!,
            Bundle = null!,
            Sidebar = null!,
            Contributor = null!
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("RazorDocs:Source must not be null.", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Failures, failure => failure.Contains("RazorDocs:Bundle must not be null.", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Failures, failure => failure.Contains("RazorDocs:Sidebar must not be null.", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Failures, failure => failure.Contains("RazorDocs:Contributor must not be null.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_ShouldRejectNullNamespacePrefixes()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Sidebar = new RazorDocsSidebarOptions
            {
                NamespacePrefixes = null!
            }
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("NamespacePrefixes must not be null", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_ShouldRequireBundlePath_WhenBundleModePathIsMissing()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Mode = RazorDocsMode.Bundle,
            Bundle = new RazorDocsBundleOptions { Path = "   " }
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("requires RazorDocs:Bundle:Path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_ShouldRejectWhitespaceSourceRepositoryRoot()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Source = new RazorDocsSourceOptions { RepositoryRoot = "   " }
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("RepositoryRoot cannot be whitespace", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void Validator_ShouldRequireDefaultBranch_WhenContributorTemplatesAreConfigured()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Contributor = new RazorDocsContributorOptions
            {
                SourceUrlTemplate = "https://example.com/blob/{branch}/{path}",
                EditUrlTemplate = "https://example.com/edit/{branch}/{path}",
                DefaultBranch = "   "
            }
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("DefaultBranch is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_ShouldRequirePathToken_WhenSourceTemplateIsConfigured()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Contributor = new RazorDocsContributorOptions
            {
                DefaultBranch = "main",
                SourceUrlTemplate = "https://example.com/blob/{branch}/docs-index"
            }
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("SourceUrlTemplate must contain the {path} token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_ShouldRequirePathToken_WhenEditTemplateIsConfigured()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Contributor = new RazorDocsContributorOptions
            {
                DefaultBranch = "main",
                EditUrlTemplate = "https://example.com/edit/{branch}/docs-index"
            }
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("EditUrlTemplate must contain the {path} token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_ShouldRejectUnsupportedContributorLastUpdatedMode()
    {
        var validator = new RazorDocsOptionsValidator();
        var options = new RazorDocsOptions
        {
            Contributor = new RazorDocsContributorOptions
            {
                LastUpdatedMode = (RazorDocsLastUpdatedMode)999
            }
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("Unsupported RazorDocs contributor last-updated mode", StringComparison.OrdinalIgnoreCase));
    }
}
