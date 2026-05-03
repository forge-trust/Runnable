# ForgeTrust.Runnable.Core

The foundation of the Runnable ecosystem. This package defines the core abstractions, the startup pipeline, and the module system that powers all other Runnable libraries.

## Overview

The Core library is designed to be lightweight and implementation-agnostic. It provides the infrastructure to:
- Define **Modules** (`IRunnableModule`, `IRunnableHostModule`) that encapsulate logic.
- Manage **Dependency Graphs** between modules.
- Provide a consistent **Startup Pipeline** (`RunnableStartup`) that sits on top of the .NET Generic Host.

## Key Concepts

- **`IRunnableModule`**: The base interface for any unit of functionality that needs to register services or configure the application.
- **`StartupContext`**: Provides metadata about the running application, including the user-facing application label, assembly-backed host identity, environment, entry point assembly, and startup-level console output mode.
- **`ConsoleOutputMode`**: Shared core enum that lets console-oriented packages describe whether command output should remain host-centric or command-first.
- **`RunnableStartup`**: The base class that orchestrates the host building and service registration process.

## Application labels and host identity

`StartupContext.ApplicationName` is a display label. Use it for generated documentation titles, command output, OpenAPI branding, and other user-facing product surfaces.

`StartupContext.HostApplicationName` is the assembly-backed identity assigned to `IHostEnvironment.ApplicationName` and the Generic Host `applicationName` setting. It defaults to the entry point assembly name, or the root module assembly name when no entry point override is configured.

Keep these values separate. ASP.NET static web assets use the host application name to find runtime manifests. Passing a custom display label such as `CustomDocsHost` into the host environment can make static asset requests resolve against a manifest that does not exist. When a test or custom host needs a different manifest identity, set `StartupContext.OverrideEntryPointAssembly` instead of overloading `ApplicationName`.

## Usage

Most users will use a more specialized package like `ForgeTrust.Runnable.Web` or `ForgeTrust.Runnable.Console`, which inherit from the abstractions provided here.

---
[🏠 Back to Root](../README.md)
