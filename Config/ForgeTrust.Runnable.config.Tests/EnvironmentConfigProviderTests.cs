using FakeItEasy;
using ForgeTrust.Runnable.Config;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.config.Tests;

public class EnvironmentConfigProviderTests
{
    [Fact]
    public void GetValue_UsesEnvironmentSpecificVariableFirst()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION_FEATURE_ENABLED", A<string?>._))
            .Returns("true");

        var provider = new EnvironmentConfigProvider(innerProvider);

        var value = provider.GetValue<bool>("Production", "Feature.Enabled");

        Assert.True(value);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION_FEATURE_ENABLED", A<string?>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("FEATURE_ENABLED", A<string?>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void GetValue_FallsBackToKeyWhenEnvironmentSpecificVariableMissing()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("DEV_US_SECTION_VALUE", A<string?>._))
            .Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("SECTION_VALUE", A<string?>._))
            .Returns("42");

        var provider = new EnvironmentConfigProvider(innerProvider);

        var value = provider.GetValue<int>("Dev-Us", "Section.Value");

        Assert.Equal(42, value);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("DEV_US_SECTION_VALUE", A<string?>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("SECTION_VALUE", A<string?>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Properties_AreProxiedToInnerProvider()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.Environment).Returns("Test");
        A.CallTo(() => innerProvider.IsDevelopment).Returns(true);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("ANY", A<string?>._))
            .Returns("value");

        var provider = new EnvironmentConfigProvider(innerProvider);

        Assert.Equal("Test", provider.Environment);
        Assert.True(provider.IsDevelopment);
        Assert.Equal("value", provider.GetEnvironmentVariable("ANY"));
    }
}
