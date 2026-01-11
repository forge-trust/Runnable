# Runnable

> ⚠️ **Under Construction:** This library is actively being developed and is not intended for production use yet.
> Monorepo for the ForgeTrust.Runnable projects

ForgeTrust.Runnable is a collection of .NET libraries designed to provide a lightweight, modular startup pipeline for both console and web applications.

## Vision

The primary vision of Runnable is to simplify application bootstrapping by encouraging **composition through small, focused modules**. Instead of monolithic startup classes or scattered configuration logic, Runnable allows developers to encapsulate features into reusable modules that handle:

-   Dependency Injection (DI) registration
-   Host configuration
-   Application-specific startup logic

This approach aims to:
-   **Share cross-cutting concerns** between different application types (e.g., sharing logging or database setup between a Web API and a background Console worker).
-   **Keep applications minimal**, with infrastructure heavily decoupled from business logic.
-   **Provide consistency** in how applications are initialized and configured, regardless of whether they are web or console apps.

## Key Design Goals

1.  **Modularity**: Everything should be a module that does one thing well. Take what you need and don't get burdened by what you don't.
2.  **Consistency**: A unified `RunnableStartup` pipeline for different project types.
3.  **Flexibility**: Open for integration with external libraries (Autofac, OpenApi, etc.) and stick to framework provided abstractions where possible.
4.  **Performance**: Designed to have minimal overhead on the application startup and execution.
5.  **Ease of Use**: Simple APIs and clear patterns to make getting started frictionless.
6.  **Convention over Configuration**: Sensible defaults are provided so only minimal configuration is required.
7.  **Secure By Default**: Security best practices are applied automatically where appropriate.


## Project Structure

### Core
- **ForgeTrust.Runnable.Core** – Core abstractions for defining modules and starting an application via `RunnableStartup` and `StartupContext`.

### Console
- **ForgeTrust.Runnable.Console** – Helpers for building command line apps with [CliFx](https://github.com/Tyrrrz/CliFx) including a `CriticalService` based command runner and helpers for configuring services.

### Web
- **ForgeTrust.Runnable.Web** – Bootstraps ASP.NET Core minimal API apps and lets modules register middleware, endpoints, and perform additional host configuration.
- **ForgeTrust.Runnable.Web.OpenApi** – Optional module that adds OpenAPI generation using `AddEndpointsApiExplorer` and `WithOpenApi`.
- **ForgeTrust.Runnable.Web.Scalar** – Optional module that serves the Scalar API reference UI and depends on the OpenAPI module.

### Dependency
- **ForgeTrust.Runnable.Dependency.Autofac** – Optional integration with the Autofac IoC container so modules can participate in Autofac service registration.

### Aspire
- **ForgeTrust.Runnable.Aspire** – Integration with .NET Aspire to provide a modular approach to defining distributed applications and service defaults.

These packages are designed to work together so that features can be shared
across different application types while maintaining a consistent startup
approach.

## Getting started

Clone the repository and build the solution:

```bash
dotnet build
dotnet test --no-build
```

Check out the examples to see how modules are composed in practice:

```bash
dotnet run --project examples/console-app
dotnet run --project examples/web-app
```

## Examples

The [examples](examples) directory contains sample applications that demonstrate
how to use this project.

- [Console app example](examples/console-app) – builds a simple command line
  application using [CliFx](https://github.com/Tyrrrz/CliFx) for command
  definitions.
- [Web app example](examples/web-app) – shows a minimal ASP.NET Core app that
  composes middleware and endpoints from modules.

## License

Runnable is licensed under the [Polyform Small Business License 1.0.0](./LICENSE).

Free for individuals and businesses with fewer than 100 people and under
$1,000,000 USD prior-year revenue (inflation-adjusted). Larger companies
require a commercial license.
