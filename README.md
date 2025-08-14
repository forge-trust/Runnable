# Runnable

> ⚠️ **Under Construction:** This library is actively being developed and is not intended for production use yet.
> Monorepo for the ForgeTrust.Runnable projects

ForgeTrust.Runnable is a collection of .NET libraries that provide a lightweight, modular
startup pipeline for console and web applications. The goal of the project is to make it
easy to compose applications from small, focused modules that can configure
dependency injection, host configuration and application specific behavior. The
libraries sit on top of the generic host so that modules can plug into the
service collection and hosting pipeline in a consistent way across project
types.

## Design goals

- Encourage composition through small, focused modules that do one thing well.
- Share cross-cutting features between console and web apps without duplicating
  bootstrapping logic.
- Keep infrastructure dependencies light so applications can stay minimal.
- Allow drop-in integrations with external libraries such as different DI
  containers or OpenAPI generators.

## Project structure

- **ForgeTrust.Runnable.Core** – core abstractions for defining modules and starting
  an application via `RunnableStartup` and `StartupContext`.
- **ForgeTrust.Runnable.Console** – helpers for building command line apps with
  [CliFx](https://github.com/Tyrrrz/CliFx) including a `CriticalService` based
  command runner and helpers for configuring services.
- **ForgeTrust.Runnable.Web** – bootstraps ASP.NET Core minimal API apps and
  lets modules register middleware, endpoints and perform additional host
  configuration.
- **ForgeTrust.Runnable.Web.OpenApi** – optional module that adds OpenAPI
  generation using `AddEndpointsApiExplorer` and `WithOpenApi`.
- **ForgeTrust.Runnable.Web.Scalar** – optional module that serves the Scalar
  API reference UI and depends on the OpenAPI module.
- **ForgeTrust.Runnable.Autofac** – integration with the Autofac IoC container
  so modules can participate in Autofac service registration.

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
