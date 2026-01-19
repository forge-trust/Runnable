# Runnable Web

The **ForgeTrust.Runnable.Web** package provides the bootstrapping logic for building ASP.NET Core applications using the Runnable module system. It sits on top of the compilation concepts defined in `ForgeTrust.Runnable.Core`.

## Overview

The easiest way to get started is by using the `WebApp` static entry point. This provides a default setup that works for most applications.

```csharp
await WebApp<MyRootModule>.RunAsync(args);
```

For more advanced use cases where you need to customize the startup lifecycle beyond what the options provide, you can extend `WebStartup<TModule>`.

## Key Abstractions

### `WebApp`

The primary entry point for web applications. It handles creating the internal startup class and running the application. It provides a generic overload `WebApp<TModule>` for standard usage and `WebApp<TStartup, TModule>` if you have a custom startup class.

### `IRunnableWebModule`

Modules that want to participate in the web startup lifecycle should implement this interface. It extends `IRunnableHostModule` and adds web-specific hooks:

*   **`ConfigureWebOptions`**: Modify the global `WebOptions` (e.g., enable MVC, configure CORS).
*   **`ConfigureWebApplication`**: Register middleware using `IApplicationBuilder` (e.g., `app.UseAuthentication()`).
*   **`ConfigureEndpoints`**: Map endpoints using `IEndpointRouteBuilder` (e.g., `endpoints.MapGet("/", ...)`).

### `WebStartup`

The base class for the application bootstrapping logic. While `WebApp` uses a generic version of this internally, you can extend it if you need deep customization of the host builder or service configuration logic.

## Features

### MVC and Controllers

Support for MVC approaches can be configured via `WebOptions`:

*   **None**: For pure Minimal APIs (default).
*   **Controllers**: For Web APIs using controllers (`AddControllers`).
*   **ControllersWithViews**: For traditional MVC apps with views.
*   **Full**: Full MVC support.

### CORS
Built-in support for CORS configuration:
*   **Enforced Origin Safety**: When `EnableCors` is true, you MUST specify at least one origin in `AllowedOrigins`. If left empty, the application will throw a startup exception to prevent unintended security openness.
*   **Development Convenience**: `EnableAllOriginsInDevelopment` (enabled by default) automatically allows any origin when the environment is `Development`, simplifying local testing without compromising production security.
*   **Default Policy**: Configures a policy named "DefaultCorsPolicy" (configurable) and automatically registers the CORS middleware.

### Endpoint Routing

Modules can define their own endpoints, making it easy to slice features vertically ("Vertical Slice Architecture").

---
[📂 Back to Web List](../README.md) | [🏠 Back to Root](../../README.md)
