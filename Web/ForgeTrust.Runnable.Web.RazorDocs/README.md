# ForgeTrust.Runnable.Web.RazorDocs

Documentation site generation and hosting for Runnable web applications.

## Overview

`ForgeTrust.Runnable.Web.RazorDocs` is the reusable Razor Class Library package behind the RazorDocs experience. It aggregates Markdown and C# API documentation into a browsable `/docs` UI and is intended to be embedded into Runnable web applications or used by the standalone RazorDocs host.

## What It Provides

- `RazorDocsWebModule` for wiring the docs UI into a Runnable web host
- `AddRazorDocs()` for typed options binding and core service registration
- `DocAggregator` plus the built-in Markdown and C# harvesters
- Search UI assets and the `/docs` MVC surface used by RazorDocs consumers
- Precompiled Tailwind-powered styling with layout-time path resolution for root-module and embedded hosts

## Configuration

Source-backed docs are configured via `RazorDocsOptions`:

```json
{
  "RazorDocs": {
    "Mode": "Source",
    "Source": {
      "RepositoryRoot": "/path/to/repo"
    }
  }
}
```

If `RazorDocs:Source:RepositoryRoot` is omitted, the package falls back to repository discovery from the app content root. Bundle mode is modeled but intentionally rejected until the next slice lands.

## Usage

Reference the package and add the module to your Runnable web application:

```csharp
await WebApp<RazorDocsWebModule>.RunAsync(args);
```

## Notes

- This package is the reusable documentation surface; `ForgeTrust.Runnable.Web.RazorDocs.Standalone` is the thin executable wrapper used for local hosting and export scenarios.
- The bundled RazorDocs UI already includes its generated stylesheet as a static web asset. The layout resolves the correct stylesheet path automatically from the host's root module shape for standalone/root-module hosts versus embedded application-part consumers.
- Consumers do not need to call `services.AddTailwind()` unless they also want Tailwind build/watch integration for their own host application's CSS.
- It depends on the Tailwind package family for RazorDocs package build-time styling generation and on the caching package for docs aggregation performance.
