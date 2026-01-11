# ForgeTrust.Runnable.Web.Scalar

This package integrates the [Scalar](https://scalar.com/) API Reference UI into Runnable web applications.

## Overview

The `RunnableWebScalarModule` provides a modern, interactive API documentation interface. It depends on `ForgeTrust.Runnable.Web.OpenApi` and automatically configures everything needed to serve the Scalar UI.

## Usage

Simply add the `RunnableWebScalarModule` to your module dependencies:

```csharp
public class MyModule : IRunnableWebModule
{
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RunnableWebScalarModule>();
    }
    
    // ...
}
```

## Features

- **Built-in Dependency**: Automatically registers the `RunnableWebOpenApiModule`.
- **Automatic UI Mapping**: Maps the Scalar API reference endpoint using `MapScalarApiReference()`.
- **Zero Config**: Works out of the box with the default Runnable startup pipeline.
