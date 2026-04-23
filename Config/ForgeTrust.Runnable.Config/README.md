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

## Notes

- The package is intended to make configuration access explicit and testable.
- Environment-aware providers make it easier to layer defaults, file configuration, and deployment-specific values.
