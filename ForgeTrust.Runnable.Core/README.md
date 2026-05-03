# ForgeTrust.Runnable.Core

The foundation of the Runnable ecosystem. This package defines the core abstractions, the startup pipeline, and the module system that powers all other Runnable libraries.

## Overview

The Core library is designed to be lightweight and implementation-agnostic. it provides the infrastructure to:
- Define **Modules** (`IRunnableModule`, `IRunnableHostModule`) that encapsulate logic.
- Manage **Dependency Graphs** between modules.
- Provide a consistent **Startup Pipeline** (`RunnableStartup`) that sits on top of the .NET Generic Host.

## Key Concepts

- **`IRunnableModule`**: The base interface for any unit of functionality that needs to register services or configure the application.
- **`StartupContext`**: Provides metadata about the running application (e.g., environment, entry point assembly, and startup-level console output mode).
- **`ConsoleOutputMode`**: Shared core enum that lets console-oriented packages describe whether command output should remain host-centric or command-first.
- **`RunnableStartup`**: The base class that orchestrates the host building and service registration process.

## Logging in Static Utilities

Core static utilities stay host-agnostic: they do not reach into a global logger, service provider, or ambient startup state. When a public static helper has useful diagnostics, expose an additive overload with an explicit non-null `ILogger` parameter and keep the existing no-logger overload silent. Private shared implementations may accept `ILogger?` only to avoid duplicating logic between the silent and diagnostic paths.

Use this pattern when a helper performs fallback behavior that callers may want to audit. For example, `PathUtils.FindRepositoryRoot(startPath, logger)` logs a warning when `startPath` does not exist and repository-root discovery has to continue from the nearest existing ancestor.

Prefer ordinary dependency injection for services, modules, hosted services, and application-owned classes. The optional logger pattern is only for static helpers where injecting a service instance would make the API harder to use or force unrelated callers to construct infrastructure.

Define static helper log messages with the source-generated `[LoggerMessage]` attribute on private static partial methods. Give each message a stable event ID, level, and template. Do not use ad hoc `logger.Log...` calls for new Core diagnostics when the message is part of an intentional API behavior.

Pitfalls:

- Do not create a static `LoggerFactory` inside a utility. That couples Core to a console/logging policy the host did not choose.
- Do not throw only because a fallback warning was logged. Keep the documented return behavior unless the input contract itself is invalid.
- Do not swallow cleanup failures silently when a logger is available. Log them at `Debug` when they are intentionally suppressed to preserve the primary exception or cancellation path.

## Usage

Most users will use a more specialized package like `ForgeTrust.Runnable.Web` or `ForgeTrust.Runnable.Console`, which inherit from the abstractions provided here.

---
[🏠 Back to Root](../README.md)
