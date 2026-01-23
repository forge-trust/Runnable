using FakeItEasy;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config.Tests;

public class ConfigTests
{
    private sealed class TestConfig : Config<string>
    {
        public override string DefaultValue => "fallback";
    }

    private sealed class TestStructConfig : ConfigStruct<int>
    {
        public override int? DefaultValue => 42;
    }

    [Fact]
    public void Init_UsesDefaultValueWhenManagerReturnsNull()
    {
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();
        var config = new TestConfig();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<string>("Production", "Test.Key"))
            .Returns(null);

        ((IConfig)config).Init(configManager, environmentProvider, "Test.Key");

        Assert.True(config.HasValue);
        Assert.True(config.IsDefaultValue);
        Assert.Equal("fallback", config.Value);
    }

    [Fact]
    public void Init_PopulatesValueFromConfigManagerWhenPresent()
    {
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();
        var config = new TestConfig();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<string>("Production", "Test.Key"))
            .Returns("value");

        ((IConfig)config).Init(configManager, environmentProvider, "Test.Key");

        Assert.True(config.HasValue);
        Assert.False(config.IsDefaultValue);
        Assert.Equal("value", config.Value);
    }

    [Fact]
    public void Init_ForStructConfigUsesManagerValueWhenPresent()
    {
        var configManager = A.Fake<IConfigManager>();
        A.CallTo(() => configManager.Priority).Returns(0);
        A.CallTo(() => configManager.Name).Returns(nameof(IConfigManager));
        // Configure the generic method for int based on the user request to match behavior
        A.CallTo(() => configManager.GetValue<int>(A<string>._, A<string>._))
            .Returns(7);

        var environmentProvider = A.Fake<IEnvironmentProvider>();
        var config = new TestStructConfig();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");

        ((IConfig)config).Init(configManager, environmentProvider, "Struct.Key");

        Assert.True(config.HasValue);
        Assert.False(config.IsDefaultValue);
        Assert.Equal(7, config.Value);
    }

    private sealed class RawConfig : Config<string>
    {
    }

    private sealed class RawStructConfig : ConfigStruct<int>
    {
    }

    [Fact]
    public void Base_DefaultValue_ReturnsNull()
    {
        var config = new RawConfig();
        Assert.Null(config.DefaultValue);

        var structConfig = new RawStructConfig();
        Assert.Null(structConfig.DefaultValue);
    }

    [Fact]
    public void Init_WithNoValueAndNoDefault_SetsHasValueToFalse()
    {
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();
        var config = new RawConfig();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<string>(A<string>._, A<string>._)).Returns(null);

        ((IConfig)config).Init(configManager, environmentProvider, "Key");

        Assert.False(config.HasValue);
        Assert.True(config.IsDefaultValue);
        Assert.Null(config.Value);
    }

    [Fact]
    public void Struct_Init_PopulatesValueFromConfigManager()
    {
        var config = new RawStructConfig();
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<int>(A<string>._, A<string>._)).Returns(42);

        ((IConfig)config).Init(configManager, environmentProvider, "Key");

        Assert.True(config.HasValue);
        Assert.False(config.IsDefaultValue);
        Assert.Equal(42, config.Value);
    }

    [Fact]
    public void Struct_Init_SetsIsDefaultValueToTrueWhenMatchesDefault()
    {
        var config = new TestStructConfig(); // DefaultValue is 42
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<int>(A<string>._, A<string>._)).Returns(42);

        ((IConfig)config).Init(configManager, environmentProvider, "Key");

        Assert.True(config.HasValue);
        Assert.True(config.IsDefaultValue);
        Assert.Equal(42, config.Value);
    }
}
