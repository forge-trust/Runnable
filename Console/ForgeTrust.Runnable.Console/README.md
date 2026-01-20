# ForgeTrust.Runnable.Console

Modular bootstrapping for .NET Console applications using [CliFx](https://github.com/Tyrrrz/CliFx).

## Overview

`ForgeTrust.Runnable.Console` provides a structured way to build command-line tools. It automatically discovers and registers CliFx commands from modules and provides a hosted service to run them.

## Usage

Create a startup class that inherits from `ConsoleStartup<TModule>`:

```csharp
public class MyConsoleStartup : ConsoleStartup<MyRootModule> { }
```

In your `Program.cs`:

```csharp
await ConsoleApp<MyRootModule>.RunAsync(args);
```

## Features

- **Command Discovery**: Automatically registers classes implementing `ICommand` from the entry point assembly and dependent modules.
- **Hosted Runner**: Integrates with the .NET Generic Host to manage service lifecycles during command execution.

---
[📂 Back to Console List](../README.md) | [🏠 Back to Root](../../README.md)
