# ForgeTrust.Runnable.Aspire

.NET Aspire integration for the Runnable ecosystem.

## Overview

`ForgeTrust.Runnable.Aspire` provides a modular way to define distributed applications using .NET Aspire. It allows you to encapsulate service defaults and resource registrations into modules.

## Usage

Use `AspireApp` to start your Aspire AppHost:

```csharp
await AspireApp<MyHostModule>.RunAsync(args);
```

---
[📂 Back to Aspire List](../README.md) | [🏠 Back to Root](../../README.md)
