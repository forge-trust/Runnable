using FakeItEasy;
using ForgeTrust.Runnable.Config;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.config.Tests;

public class FileBasedConfigProviderTests
{
    [Fact]
    public void GetValue_MergesFilesByEnvironmentAndPriority()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"),
                """{"Feature":{"Enabled":false,"Name":"Prod"}}""");
            File.WriteAllText(Path.Combine(tempDir, "config_extra.json"),
                """{"Feature":{"Extra":"ProdExtra"}}""");
            File.WriteAllText(Path.Combine(tempDir, "appsettings.Development.json"),
                """{"Feature":{"Name":"Dev"}}""");
            File.WriteAllText(Path.Combine(tempDir, "config_extra.Development.json"),
                """{"Feature":{"Enabled":true,"Extra":"Value"}}""");
            File.WriteAllText(Path.Combine(tempDir, "config_bad.json"), "{not json}");

            var environmentProvider = A.Fake<IEnvironmentProvider>();
            var locationProvider = A.Fake<IConfigFileLocationProvider>();
            var logger = A.Fake<ILogger<FileBasedConfigProvider>>();

            A.CallTo(() => locationProvider.Directory).Returns(tempDir);

            var provider = new FileBasedConfigProvider(environmentProvider, locationProvider, logger);

            Assert.Equal("Dev", provider.GetValue<string>("Development", "Feature.Name"));
            Assert.True(provider.GetValue<bool>("Development", "Feature.Enabled"));
            Assert.Equal("Value", provider.GetValue<string>("Development", "Feature.Extra"));
            Assert.False(provider.GetValue<bool>("Production", "Feature.Enabled"));
            Assert.Equal("ProdExtra", provider.GetValue<string>("Production", "Feature.Extra"));
            Assert.Null(provider.GetValue<string>("Production", "Feature.Unknown"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GetValue_ReturnsDefaultWhenDirectoryMissing()
    {
        var environmentProvider = A.Fake<IEnvironmentProvider>();
        var locationProvider = A.Fake<IConfigFileLocationProvider>();
        var logger = A.Fake<ILogger<FileBasedConfigProvider>>();

        A.CallTo(() => locationProvider.Directory).Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var provider = new FileBasedConfigProvider(environmentProvider, locationProvider, logger);

        Assert.Null(provider.GetValue<string>("Production", "Any.Key"));
    }

    [Fact]
    public void GetValue_ReusesCachedConfigurationAfterInitialization()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "appsettings.json");
            File.WriteAllText(configPath, """{"Feature":{"Enabled":true}}""");

            var environmentProvider = A.Fake<IEnvironmentProvider>();
            var locationProvider = A.Fake<IConfigFileLocationProvider>();
            var logger = A.Fake<ILogger<FileBasedConfigProvider>>();

            A.CallTo(() => locationProvider.Directory).Returns(tempDir);

            var provider = new FileBasedConfigProvider(environmentProvider, locationProvider, logger);

            Assert.True(provider.GetValue<bool>("Production", "Feature.Enabled"));

            File.WriteAllText(configPath, """{"Feature":{"Enabled":false}}""");

            Assert.True(provider.GetValue<bool>("Production", "Feature.Enabled"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
