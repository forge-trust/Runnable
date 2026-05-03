# ForgeTrust.Runnable.Config

Strongly typed configuration primitives for Runnable applications.

## Overview

This package provides the configuration layer for Runnable modules. It combines file-based configuration, environment-aware providers, and strongly typed configuration objects so modules can consume configuration without hard-coding access patterns throughout the codebase.

## Key Types

- **`RunnableConfigModule`**: Registers the configuration services for a Runnable application.
- **`IConfigManager`**: Central access point for resolving configuration values.
- **`IConfigProvider`**: Abstraction for reading configuration from a source.
- **`FileBasedConfigProvider`**: Loads configuration from files.
- **`Config<T>` / `ConfigStruct<T>`**: Base types for strongly typed configuration values.
- **`ConfigKeyAttribute`**: Associates a configuration type or property with a specific key.
- **`ConfigurationValidationException`**: Startup-time exception that reports DataAnnotations failures for a resolved config value.
- **`ConfigurationValidationFailure`**: Structured validation failure details for logging and tests.

## Usage

Register the module and model your settings with strongly typed config objects:

```csharp
public sealed class MyModule : RunnableConfigModule
{
}
```

Define configuration models:

```csharp
public sealed class DocsPathConfig : Config<string>
{
}
```

Resolve them through the configuration services used by your module or application startup flow.

## DataAnnotations Validation

`Config<T>` and `ConfigStruct<T>` validate the resolved value during initialization when the value is present. Validation runs after provider/default resolution, so defaults are held to the same rules as provider-supplied values. Optional `ConfigStruct<T>` values resolve through nullable `T?` provider lookups so a missing struct value is not confused with a configured zero-initialized value.

Use ordinary DataAnnotations on object-valued config models:

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class RetryOptions
{
    [Range(1, 5)]
    public int Count { get; init; }
}

public sealed class RetryConfig : Config<RetryOptions>
{
}
```

When validation fails, Runnable throws one `ConfigurationValidationException` for the config key. The exception message is designed for logs:

```text
Configuration validation failed for key 'RetryConfig' (RetryConfig -> RetryOptions): 1 error(s).
- Count: The field Count must be between 1 and 5.
```

The exception also exposes structured failures through `Failures`. Each failure includes the config key, wrapper type, value type, member names, and validation message. Attempted values are not exposed because config values often include secrets.

### Recursive Validation

Top-level validation follows the normal DataAnnotations runtime behavior. Nested objects and collection items are validated only when explicitly marked with Microsoft Options validation attributes:

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

public sealed class DatabaseOptions
{
    [Required]
    public string? Host { get; init; }
}

public sealed class EndpointOptions
{
    [Required]
    public string? Url { get; init; }
}

public sealed class AppOptions
{
    [ValidateObjectMembers]
    public DatabaseOptions? Database { get; init; }

    [ValidateEnumeratedItems]
    public List<EndpointOptions> Endpoints { get; init; } = [];
}

public sealed class AppConfig : Config<AppOptions>
{
}
```

Nested member paths are reported with dot and index notation, such as `Database.Host` and `Endpoints[0].Url`. Recursive validation tracks the active traversal path so cycles do not loop forever while repeated references are still reported at each distinct reachable path. Null nested objects and null collection items are skipped unless the containing property has `[Required]`.

Runnable uses the Microsoft marker attributes as the public authoring contract, but it owns the runtime traversal and `ConfigurationValidationException` output. It does not invoke the Options source generator or the `IValidateOptions<TOptions>` pipeline.

### Pitfalls

- Scalar primitive configs such as `Config<string>` and `ConfigStruct<int>` are not meaningful DataAnnotations targets. Wrap scalar values in an options object when validation matters.
- DataAnnotations can short-circuit. For example, object-level `IValidatableObject` validation may not run when property validation already failed.
- Recursive validation is opt-in. A nested object without `[ValidateObjectMembers]` and a collection without `[ValidateEnumeratedItems]` is not traversed.
- The `Validator` constructor overloads on Microsoft Options validation attributes are not supported by Runnable Config validation.

## Notes

- The package is intended to make configuration access explicit and testable.
- Environment-aware providers make it easier to layer defaults, file configuration, and deployment-specific values.
