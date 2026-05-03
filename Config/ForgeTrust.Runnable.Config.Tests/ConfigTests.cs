using System.ComponentModel.DataAnnotations;
using FakeItEasy;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.Options;

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

    private sealed class AnnotatedOptions
    {
        [Required]
        public string? Name { get; init; }

        [Range(1, 5)]
        public int RetryCount { get; init; }
    }

    private sealed class AnnotatedOptionsConfig : Config<AnnotatedOptions>
    {
    }

    private sealed class DefaultAnnotatedOptionsConfig : Config<AnnotatedOptions>
    {
        public override AnnotatedOptions DefaultValue { get; } = new()
        {
            Name = "default",
            RetryCount = 0
        };
    }

    private sealed class ValidatableOptions : IValidatableObject
    {
        public bool Invalid { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Invalid)
            {
                yield return new ValidationResult("Object is invalid.");
            }
        }
    }

    private sealed class MultiMemberValidatableOptions : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield return new ValidationResult("Both values are invalid.", ["Second", "First"]);
        }
    }

    private sealed class ShortCircuitOptions : IValidatableObject
    {
        public static bool ObjectValidationRan { get; set; }

        [Required]
        public string? Name { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            ObjectValidationRan = true;
            yield return new ValidationResult("Object validation ran.");
        }
    }

    private sealed class ValidatableOptionsConfig : Config<ValidatableOptions>
    {
    }

    private sealed class MultiMemberValidatableOptionsConfig : Config<MultiMemberValidatableOptions>
    {
    }

    private sealed class ShortCircuitOptionsConfig : Config<ShortCircuitOptions>
    {
    }

    private sealed class NestedOptions
    {
        [Required]
        public string? Host { get; init; }
    }

    private sealed class EndpointOptions
    {
        [Required]
        public string? Url { get; init; }
    }

    private sealed class RecursiveOptions
    {
        [ValidateObjectMembers]
        public NestedOptions? Database { get; init; }

        [ValidateEnumeratedItems]
        public List<EndpointOptions?> Endpoints { get; init; } = [];
    }

    private sealed class NonRecursiveOptions
    {
        public NestedOptions? Database { get; init; }

        public List<EndpointOptions?> Endpoints { get; init; } = [];
    }

    private sealed class OptionsWithUnmarkedThrowingGetter
    {
        [Required]
        public string? Name { get; init; }

        public NestedOptions Database => throw new InvalidOperationException("Getter should not run.");
    }

    private sealed class RecursiveFieldOptions
    {
        [ValidateObjectMembers]
        public NestedOptions? Database = new();

        [ValidateEnumeratedItems]
        public List<EndpointOptions?> Endpoints = [];
    }

    private sealed class NestedValidatableOptions : IValidatableObject
    {
        public bool Invalid { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Invalid)
            {
                yield return new ValidationResult("Nested object is invalid.");
            }
        }
    }

    private sealed class OptionsWithNestedObjectLevelValidation
    {
        [ValidateObjectMembers]
        public NestedValidatableOptions Child { get; init; } = new();
    }

    private sealed class OptionsWithNullMarkedMembers
    {
        [ValidateObjectMembers]
        public NestedOptions? Database { get; init; }

        [ValidateEnumeratedItems]
        public List<EndpointOptions?>? Endpoints { get; init; }
    }

    private sealed class UnsupportedEnumeratedItemsValidatorOptions
    {
        [ValidateEnumeratedItems(typeof(object))]
        public List<EndpointOptions> Endpoints { get; init; } = [];
    }

    private readonly struct AnnotatedStructOptions
    {
        [Required]
        public string? Name { get; init; }

        [Range(1, 5)]
        public int RetryCount { get; init; }
    }

    private sealed class AnnotatedStructOptionsConfig : ConfigStruct<AnnotatedStructOptions>
    {
    }

    private sealed class DefaultAnnotatedStructOptionsConfig : ConfigStruct<AnnotatedStructOptions>
    {
        public override AnnotatedStructOptions? DefaultValue => new()
        {
            Name = "default",
            RetryCount = 0
        };
    }

    private readonly struct StructNestedOptions
    {
        [Required]
        public string? Name { get; init; }
    }

    private sealed class OptionsWithStructNestedObject
    {
        [ValidateObjectMembers]
        public StructNestedOptions Child { get; init; }
    }

    private sealed class RecursiveOptionsConfig : Config<RecursiveOptions>
    {
    }

    private sealed class NonRecursiveOptionsConfig : Config<NonRecursiveOptions>
    {
    }

    private sealed class OptionsWithUnmarkedThrowingGetterConfig : Config<OptionsWithUnmarkedThrowingGetter>
    {
    }

    private sealed class RecursiveFieldOptionsConfig : Config<RecursiveFieldOptions>
    {
    }

    private sealed class OptionsWithNestedObjectLevelValidationConfig : Config<OptionsWithNestedObjectLevelValidation>
    {
    }

    private sealed class OptionsWithNullMarkedMembersConfig : Config<OptionsWithNullMarkedMembers>
    {
    }

    private sealed class UnsupportedEnumeratedItemsValidatorOptionsConfig : Config<UnsupportedEnumeratedItemsValidatorOptions>
    {
    }

    private sealed class OptionsWithStructNestedObjectConfig : Config<OptionsWithStructNestedObject>
    {
    }

    private sealed class OptionsWithSharedReferences
    {
        [ValidateObjectMembers]
        public NestedOptions? Primary { get; init; }

        [ValidateObjectMembers]
        public NestedOptions? Secondary { get; init; }
    }

    private sealed class OptionsWithSharedReferencesConfig : Config<OptionsWithSharedReferences>
    {
    }

    private sealed class CyclicOptions
    {
        [ValidateObjectMembers]
        public CyclicOptions? Next { get; set; }
    }

    private sealed class CyclicOptionsConfig : Config<CyclicOptions>
    {
    }

    private sealed class UnsupportedValidatorOptions
    {
        [ValidateObjectMembers(typeof(object))]
        public NestedOptions Child { get; init; } = new();
    }

    private sealed class UnsupportedValidatorOptionsConfig : Config<UnsupportedValidatorOptions>
    {
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
        A.CallTo(() => configManager.GetValue<int?>(A<string>._, A<string>._))
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
        A.CallTo(() => configManager.GetValue<int?>(A<string>._, A<string>._)).Returns(42);

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
        A.CallTo(() => configManager.GetValue<int?>(A<string>._, A<string>._)).Returns(42);

        ((IConfig)config).Init(configManager, environmentProvider, "Key");

        Assert.True(config.HasValue);
        Assert.True(config.IsDefaultValue);
        Assert.Equal(42, config.Value);
    }

    [Fact]
    public void Struct_Init_WithMissingValueAndNoDefault_DoesNotValidateDefaultStruct()
    {
        var config = new AnnotatedStructOptionsConfig();
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<AnnotatedStructOptions?>("Production", "Struct.Settings"))
            .Returns(null);

        ((IConfig)config).Init(configManager, environmentProvider, "Struct.Settings");

        Assert.False(config.HasValue);
        Assert.True(config.IsDefaultValue);
        Assert.Null(config.Value);
    }

    [Fact]
    public void Init_WithValidAnnotatedObject_PopulatesValue()
    {
        var config = new AnnotatedOptionsConfig();
        var value = new AnnotatedOptions
        {
            Name = "payments",
            RetryCount = 3
        };

        Init(config, value);

        Assert.True(config.HasValue);
        Assert.False(config.IsDefaultValue);
        Assert.Same(value, config.Value);
    }

    [Fact]
    public void Init_WithInvalidAnnotatedObject_ThrowsStructuredValidationException()
    {
        var config = new AnnotatedOptionsConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(config, new AnnotatedOptions { RetryCount = 7 }));

        Assert.Equal("App.Settings", exception.Key);
        Assert.Equal(typeof(AnnotatedOptionsConfig), exception.ConfigType);
        Assert.Equal(typeof(AnnotatedOptions), exception.ValueType);
        Assert.Equal(2, exception.Failures.Count);
        Assert.Contains(
            exception.Failures,
            failure => failure.MemberNames.SequenceEqual(["Name"])
                       && failure.Message.Contains("Name", StringComparison.Ordinal));
        var nameFailure = Assert.Single(exception.Failures, failure => failure.MemberNames.SequenceEqual(["Name"]));
        Assert.Equal("App.Settings", nameFailure.Key);
        Assert.Equal(typeof(AnnotatedOptionsConfig), nameFailure.ConfigType);
        Assert.Equal(typeof(AnnotatedOptions), nameFailure.ValueType);
        Assert.Contains(
            exception.Failures,
            failure => failure.MemberNames.SequenceEqual(["RetryCount"])
                       && failure.Message.Contains("between 1 and 5", StringComparison.Ordinal));
        Assert.DoesNotContain("7", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationFailures_AreImmutableSnapshots()
    {
        var memberNames = new List<string> { "Name" };
        var failure = new ConfigurationValidationFailure(
            "App.Settings",
            typeof(AnnotatedOptionsConfig),
            typeof(AnnotatedOptions),
            memberNames,
            "Name is required.");
        var failures = new List<ConfigurationValidationFailure> { failure };

        var exception = new ConfigurationValidationException(
            "App.Settings",
            typeof(AnnotatedOptionsConfig),
            typeof(AnnotatedOptions),
            failures);

        memberNames.Add("RetryCount");
        failures.Clear();

        Assert.Equal(["Name"], failure.MemberNames);
        Assert.Single(exception.Failures);
        Assert.Throws<NotSupportedException>(() => ((ICollection<string>)failure.MemberNames).Add("Other"));
        Assert.Throws<NotSupportedException>(() => ((ICollection<ConfigurationValidationFailure>)exception.Failures).Clear());
    }

    [Fact]
    public void Init_WithInvalidDefault_ValidatesDefaultAndThrows()
    {
        var config = new DefaultAnnotatedOptionsConfig();
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<AnnotatedOptions>(A<string>._, A<string>._))
            .Returns(null);

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            ((IConfig)config).Init(configManager, environmentProvider, "App.Settings"));

        Assert.True(config.HasValue);
        Assert.True(config.IsDefaultValue);
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["RetryCount"]));
    }

    [Fact]
    public void Struct_Init_WithInvalidAnnotatedObject_ThrowsStructuredValidationException()
    {
        var config = new AnnotatedStructOptionsConfig();
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<AnnotatedStructOptions?>("Production", "Struct.Settings"))
            .Returns(new AnnotatedStructOptions { RetryCount = 7 });

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            ((IConfig)config).Init(configManager, environmentProvider, "Struct.Settings"));

        Assert.Equal("Struct.Settings", exception.Key);
        Assert.Equal(typeof(AnnotatedStructOptionsConfig), exception.ConfigType);
        Assert.Equal(typeof(AnnotatedStructOptions), exception.ValueType);
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["Name"]));
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["RetryCount"]));
    }

    [Fact]
    public void Struct_Init_WithInvalidDefault_ValidatesDefaultAndThrows()
    {
        var config = new DefaultAnnotatedStructOptionsConfig();
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<AnnotatedStructOptions?>("Production", "Struct.Settings"))
            .Returns(null);

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            ((IConfig)config).Init(configManager, environmentProvider, "Struct.Settings"));

        Assert.True(config.HasValue);
        Assert.True(config.IsDefaultValue);
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["RetryCount"]));
    }

    [Fact]
    public void Init_WithObjectLevelValidationFailure_UsesObjectFailureLabel()
    {
        var config = new ValidatableOptionsConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(config, new ValidatableOptions { Invalid = true }));

        var failure = Assert.Single(exception.Failures);
        Assert.Empty(failure.MemberNames);
        Assert.Contains("- <object>: Object is invalid.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_WithMultipleMemberValidationResult_PreservesAllMemberNames()
    {
        var config = new MultiMemberValidatableOptionsConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(config, new MultiMemberValidatableOptions()));

        var failure = Assert.Single(exception.Failures);
        Assert.Equal(["Second", "First"], failure.MemberNames);
        Assert.Contains("- First, Second: Both values are invalid.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_WithPropertyFailures_DoesNotForceObjectLevelValidation()
    {
        ShortCircuitOptions.ObjectValidationRan = false;
        var config = new ShortCircuitOptionsConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(config, new ShortCircuitOptions()));

        Assert.False(ShortCircuitOptions.ObjectValidationRan);
        Assert.Single(exception.Failures);
    }

    [Fact]
    public void Init_WithNestedOptionsWithoutMarkers_DoesNotValidateRecursively()
    {
        var config = new NonRecursiveOptionsConfig();

        Init(
            config,
            new NonRecursiveOptions
            {
                Database = new NestedOptions(),
                Endpoints = [new EndpointOptions()]
            });

        Assert.True(config.HasValue);
    }

    [Fact]
    public void Init_WithUnmarkedThrowingGetter_DoesNotInvokeGetter()
    {
        var config = new OptionsWithUnmarkedThrowingGetterConfig();

        Init(config, new OptionsWithUnmarkedThrowingGetter { Name = "payments" });

        Assert.True(config.HasValue);
    }

    [Fact]
    public void Init_WithRecursiveMarkers_ValidatesNestedObjectAndCollectionItems()
    {
        var config = new RecursiveOptionsConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(
                config,
                new RecursiveOptions
                {
                    Database = new NestedOptions(),
                    Endpoints = [new EndpointOptions(), null, new EndpointOptions { Url = "https://example.test" }]
                }));

        Assert.Equal(2, exception.Failures.Count);
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["Database.Host"]));
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["Endpoints[0].Url"]));
        Assert.DoesNotContain("Endpoints[1]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_WithRecursiveMarkersOnFields_ValidatesNestedObjectAndCollectionItems()
    {
        var config = new RecursiveFieldOptionsConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(
                config,
                new RecursiveFieldOptions
                {
                    Database = new NestedOptions(),
                    Endpoints = [new EndpointOptions()]
                }));

        Assert.Equal(2, exception.Failures.Count);
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["Database.Host"]));
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["Endpoints[0].Url"]));
    }

    [Fact]
    public void Init_WithNestedObjectLevelValidationFailure_UsesNestedObjectPath()
    {
        var config = new OptionsWithNestedObjectLevelValidationConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(
                config,
                new OptionsWithNestedObjectLevelValidation
                {
                    Child = new NestedValidatableOptions { Invalid = true }
                }));

        var failure = Assert.Single(exception.Failures);
        Assert.Equal(["Child"], failure.MemberNames);
        Assert.Contains("- Child: Nested object is invalid.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_WithNullMarkedMembers_SkipsRecursiveValidation()
    {
        var config = new OptionsWithNullMarkedMembersConfig();

        Init(config, new OptionsWithNullMarkedMembers());

        Assert.True(config.HasValue);
    }

    [Fact]
    public void Init_WithUnsupportedEnumeratedItemsValidatorType_ReportsFailure()
    {
        var config = new UnsupportedEnumeratedItemsValidatorOptionsConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(config, new UnsupportedEnumeratedItemsValidatorOptions()));

        Assert.Contains(
            exception.Failures,
            failure => failure.MemberNames.SequenceEqual(["Endpoints"])
                       && failure.Message.Contains(nameof(ValidateEnumeratedItemsAttribute), StringComparison.Ordinal));
    }

    [Fact]
    public void Init_WithRecursiveStructMember_ValidatesBoxedValueType()
    {
        var config = new OptionsWithStructNestedObjectConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(config, new OptionsWithStructNestedObject()));

        var failure = Assert.Single(exception.Failures);
        Assert.Equal(["Child.Name"], failure.MemberNames);
    }

    [Fact]
    public void Init_WithSharedRecursiveReference_ReportsEachReachablePath()
    {
        var config = new OptionsWithSharedReferencesConfig();
        var shared = new NestedOptions();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(
                config,
                new OptionsWithSharedReferences
                {
                    Primary = shared,
                    Secondary = shared
                }));

        Assert.Equal(2, exception.Failures.Count);
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["Primary.Host"]));
        Assert.Contains(exception.Failures, failure => failure.MemberNames.SequenceEqual(["Secondary.Host"]));
    }

    [Fact]
    public void Init_WithCyclicRecursiveMarkers_DoesNotLoopForever()
    {
        var config = new CyclicOptionsConfig();
        var value = new CyclicOptions();
        value.Next = value;

        Init(config, value);

        Assert.True(config.HasValue);
    }

    [Fact]
    public void Init_WithUnsupportedOptionsValidatorType_ReportsFailure()
    {
        var config = new UnsupportedValidatorOptionsConfig();

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            Init(config, new UnsupportedValidatorOptions()));

        Assert.Contains(
            exception.Failures,
            failure => failure.MemberNames.SequenceEqual(["Child"])
                       && failure.Message.Contains("custom validator types are not supported", StringComparison.Ordinal));
    }

    [Fact]
    public void Init_WithScalarPrimitiveConfig_DoesNotRunDataAnnotations()
    {
        var config = new TestConfig();

        Init(config, string.Empty);

        Assert.True(config.HasValue);
        Assert.Equal(string.Empty, config.Value);
    }

    private static void Init<T>(Config<T> config, T? value)
        where T : class
    {
        var configManager = A.Fake<IConfigManager>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();

        A.CallTo(() => environmentProvider.Environment).Returns("Production");
        A.CallTo(() => configManager.GetValue<T>("Production", "App.Settings"))
            .Returns(value);

        ((IConfig)config).Init(configManager, environmentProvider, "App.Settings");
    }
}
