# ForgeTrust.Runnable.Caching

Caching primitives for Runnable applications built on top of `Microsoft.Extensions.Caching.Memory`.

## Overview

This package provides a small, focused caching layer for Runnable modules. It is designed for scenarios where you want consistent memoization behavior, cache policies, and a module you can register into the Runnable startup pipeline.

## Key Types

- **`RunnableCachingModule`**: Registers the package services into the Runnable module system.
- **`IMemo` / `Memo`**: Memoization helpers for caching computed values and async results.
- **`CachePolicy`**: A simple policy object for configuring expiration and cache behavior.

## Usage

Register the module in your application and inject `IMemo` where you want to cache repeated work:

```csharp
public sealed class MyModule : RunnableCachingModule
{
}
```

Use memoization for expensive or repeated lookups:

```csharp
var result = await memo.GetOrCreateAsync(
    "docs:index",
    () => LoadDocsAsync(),
    new CachePolicy());
```

## Notes

- The package builds on `Microsoft.Extensions.Caching.Memory`, so it works well for in-process application caching.
- This package is intentionally lightweight and fits best when you want simple, application-level caching rather than a distributed cache abstraction.
