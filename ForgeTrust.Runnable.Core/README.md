# ForgeTrust.Runnable.Core

The foundation of the Runnable ecosystem. This package defines the core abstractions, the startup pipeline, and the module system that powers all other Runnable libraries.

## Overview

The Core library is designed to be lightweight and implementation-agnostic. it provides the infrastructure to:
- Define **Modules** (`IRunnableModule`, `IRunnableHostModule`) that encapsulate logic.
- Manage **Dependency Graphs** between modules.
- Provide a consistent **Startup Pipeline** (`RunnableStartup`) that sits on top of the .NET Generic Host.

## Key Concepts

- **`IRunnableModule`**: The base interface for any unit of functionality that needs to register services or configure the application.
- **`StartupContext`**: Provides metadata about the running application (e.g., environment, entry point assembly).
- **`RunnableStartup`**: The base class that orchestrates the host building and service registration process.

## Usage

Most users will use a more specialized package like `ForgeTrust.Runnable.Web` or `ForgeTrust.Runnable.Console`, which inherit from the abstractions provided here.

---
[🏠 Back to Root](../README.md)
