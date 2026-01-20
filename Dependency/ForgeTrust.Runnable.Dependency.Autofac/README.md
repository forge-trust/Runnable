# ForgeTrust.Runnable.Dependency.Autofac

Autofac IoC container integration for Runnable modules.

## Overview

This package allows modules to participate in Autofac service registration, enabling advanced DI features not available in the default .NET service collection.

## Usage

Inherit from `RunnableAutofacModule` instead of `IRunnableModule` if you need to use Autofac-specific registrations.

```csharp
public class MyAutofacModule : RunnableAutofacModule
{
    protected override void Load(ContainerBuilder builder)
    {
        // Custom Autofac registrations
    }
}
```

---
[📂 Back to Dependency List](../README.md) | [🏠 Back to Root](../../README.md)
