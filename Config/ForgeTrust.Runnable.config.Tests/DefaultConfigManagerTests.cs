using FakeItEasy;
using ForgeTrust.Runnable.Config;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.config.Tests;

public class DefaultConfigManagerTests
{
    [Fact]
    public void GetValue_ReturnsEnvironmentValueWhenPresent()
    {
        var environmentProvider = A.Fake<IEnvironmentConfigProvider>();
        var logger = A.Fake<ILogger<DefaultConfigManager>>();
        var otherProvider = A.Fake<IConfigProvider>();

        A.CallTo(() => environmentProvider.GetValue<string>("Production", "App.Key"))
            .Returns("from-environment");

        var manager = new DefaultConfigManager(environmentProvider, [otherProvider], logger);

        var value = manager.GetValue<string>("Production", "App.Key");

        Assert.Equal("from-environment", value);
        A.CallTo(() => otherProvider.GetValue<string>(A<string>._, A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void GetValue_QueriesProvidersByDescendingPriority()
    {
        var environmentProvider = A.Fake<IEnvironmentConfigProvider>();
        var logger = A.Fake<ILogger<DefaultConfigManager>>();
        var highPriorityProvider = A.Fake<IConfigProvider>();
        var lowPriorityProvider = A.Fake<IConfigProvider>();
        var anotherConfigManager = A.Fake<IConfigManager>();

        A.CallTo(() => environmentProvider.GetValue<string>(A<string>._, A<string>._))
            .Returns(null);
        A.CallTo(() => highPriorityProvider.Priority).Returns(10);
        A.CallTo(() => highPriorityProvider.GetValue<string>("Production", "Feature.Flag"))
            .Returns(null);
        A.CallTo(() => lowPriorityProvider.Priority).Returns(1);
        A.CallTo(() => lowPriorityProvider.GetValue<string>("Production", "Feature.Flag"))
            .Returns("from-low");

        var manager = new DefaultConfigManager(
            environmentProvider,
            [
                highPriorityProvider,
                lowPriorityProvider,
                environmentProvider, // should be filtered out
                anotherConfigManager // should be filtered out
            ],
            logger);

        var value = manager.GetValue<string>("Production", "Feature.Flag");

        Assert.Equal("from-low", value);

        A.CallTo(() => highPriorityProvider.GetValue<string>("Production", "Feature.Flag"))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => lowPriorityProvider.GetValue<string>("Production", "Feature.Flag"))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => environmentProvider.GetValue<string>("Production", "Feature.Flag"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GetValue_ReturnsDefaultWhenNoProvidersHaveValue()
    {
        var environmentProvider = A.Fake<IEnvironmentConfigProvider>();
        var logger = A.Fake<ILogger<DefaultConfigManager>>();
        var otherProvider = A.Fake<IConfigProvider>();

        A.CallTo(() => environmentProvider.GetValue<string>(A<string>._, A<string>._)).Returns(null);
        A.CallTo(() => otherProvider.GetValue<string>(A<string>._, A<string>._)).Returns(null);

        var manager = new DefaultConfigManager(environmentProvider, [otherProvider], logger);

        var value = manager.GetValue<string>("Any", "Missing.Key");

        Assert.Null(value);
        A.CallTo(() => environmentProvider.GetValue<string>("Any", "Missing.Key"))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => otherProvider.GetValue<string>("Any", "Missing.Key"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Constructor_HandlesNullOtherProviders()
    {
        var environmentProvider = A.Fake<IEnvironmentConfigProvider>();
        var logger = A.Fake<ILogger<DefaultConfigManager>>();

        var manager = new DefaultConfigManager(environmentProvider, null, logger);

        Assert.NotNull(manager);
    }

    [Fact]
    public void GetValue_ReturnsDefaultWithNoProviders()
    {
        var environmentProvider = A.Fake<IEnvironmentConfigProvider>();
        var logger = A.Fake<ILogger<DefaultConfigManager>>();

        // Setup environment to return null
        A.CallTo(() => environmentProvider.GetValue<string>(A<string>._, A<string>._))
            .Returns(null);

        // No other providers
        var manager = new DefaultConfigManager(environmentProvider, [], logger);

        var value = manager.GetValue<string>("Production", "Any.Key");

        Assert.Null(value);
    }
}
