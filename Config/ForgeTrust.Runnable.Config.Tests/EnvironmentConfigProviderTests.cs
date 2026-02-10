using FakeItEasy;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config.Tests;

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
    public void GetValue_ParsesEnumValuesCaseInsensitive()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION_DAY", A<string?>._)).Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("DAY", A<string?>._))
            .Returns("monday");

        var provider = new EnvironmentConfigProvider(innerProvider);

        var value = provider.GetValue<DayOfWeek>("Production", "Day");

        Assert.Equal(DayOfWeek.Monday, value);
    }

    [Fact]
    public void GetValue_HandlesNullableTypes()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        var provider = new EnvironmentConfigProvider(innerProvider);

        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION_A", A<string?>._)).Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("A", A<string?>._)).Returns("123");
        Assert.Equal(123, provider.GetValue<int?>("Production", "A"));

        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION_B", A<string?>._)).Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("B", A<string?>._)).Returns("");
        Assert.Null(provider.GetValue<int?>("Production", "B"));

        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION_C", A<string?>._)).Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("C", A<string?>._)).Returns(null);
        Assert.Null(provider.GetValue<int?>("Production", "C"));
    }

    [Fact]
    public void GetValue_ReturnsDefaultOnInvalidFormat()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("VALUE", A<string?>._))
            .Returns("not-a-number");

        var provider = new EnvironmentConfigProvider(innerProvider);

        Assert.Equal(0, provider.GetValue<int>("Production", "Value"));
    }

    [Fact]
    public void GetValue_ReturnsDefaultOnOverflow()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("VALUE", A<string?>._))
            .Returns(long.MaxValue.ToString());

        var provider = new EnvironmentConfigProvider(innerProvider);

        Assert.Equal(0, provider.GetValue<int>("Production", "Value"));
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

    [Fact]
    public void GetValue_ReturnsDefaultOnInvalidEnum()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("VALUE", A<string?>._))
            .Returns("InvalidEnumValue");

        var provider = new EnvironmentConfigProvider(innerProvider);

        Assert.Equal(default(System.UriKind), provider.GetValue<System.UriKind>("Production", "Value"));
    }

    [Fact]
    public void GetValue_BindsTopLevelListFromJsonValue()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("MYAPP_ITEMS", A<string?>._)).Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("ITEMS", A<string?>._)).Returns("""["a","b"]""");

        var provider = new EnvironmentConfigProvider(innerProvider);

        var value = provider.GetValue<List<string>>("MyApp", "Items");

        Assert.NotNull(value);
        Assert.Equal(["a", "b"], value);
    }

    [Fact]
    public void GetValue_BindsTopLevelDictionaryFromJsonValue()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION_SETTINGS", A<string?>._))
            .Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("SETTINGS", A<string?>._))
            .Returns("""{"Retries":3,"Timeout":30}""");

        var provider = new EnvironmentConfigProvider(innerProvider);

        var value = provider.GetValue<Dictionary<string, int>>("Production", "Settings");

        Assert.NotNull(value);
        Assert.Equal(3, value["Retries"]);
        Assert.Equal(30, value["Timeout"]);
    }

    [Fact]
    public void GetValue_BindsIndexedListFromDoubleUnderscoreVariables()
    {
        var innerProvider = A.Fake<IEnvironmentProvider>();
        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION_MYAPP_ITEMS", A<string?>._))
            .Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("MYAPP_ITEMS", A<string?>._))
            .Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION__MYAPP__ITEMS", A<string?>._))
            .Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("MYAPP__ITEMS", A<string?>._))
            .Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("PRODUCTION__MYAPP__ITEMS__0", A<string?>._))
            .Returns(null);
        A.CallTo(() => innerProvider.GetEnvironmentVariable("MYAPP__ITEMS__0", A<string?>._))
            .Returns("First");
        A.CallTo(() => innerProvider.GetEnvironmentVariable("MYAPP__ITEMS__1", A<string?>._))
            .Returns("Second");
        A.CallTo(() => innerProvider.GetEnvironmentVariable("MYAPP__ITEMS__2", A<string?>._))
            .Returns(null);

        var provider = new EnvironmentConfigProvider(innerProvider);

        var value = provider.GetValue<List<string>>("Production", "MyApp.Items");

        Assert.NotNull(value);
        Assert.Equal(["First", "Second"], value);
    }
}
