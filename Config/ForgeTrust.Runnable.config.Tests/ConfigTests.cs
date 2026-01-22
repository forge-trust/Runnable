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

    private sealed class IntConfigManagerStub : IConfigManager
    {
        public int Priority => 0;

        public string Name => nameof(IntConfigManagerStub);

        public int? ValueToReturn { get; set; }

        public T? GetValue<T>(string environment, string key)
        {
            if (typeof(T) == typeof(int))
            {
                return ValueToReturn is null
                    ? default
                    : (T?)(object?)ValueToReturn;
            }

            return default;
        }
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
        var configManager = new IntConfigManagerStub { ValueToReturn = 7 };
        var environmentProvider = A.Fake<IEnvironmentProvider>();
        var config = new TestStructConfig();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");

        config.Init(configManager, environmentProvider, "Struct.Key");

        Assert.True(config.HasValue);
        Assert.Equal(7, config.Value);
    }
}
