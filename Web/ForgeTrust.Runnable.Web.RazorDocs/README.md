# ForgeTrust.Runnable.Web.RazorDocs

Documentation site generation and hosting for Runnable web applications.

## Overview

`ForgeTrust.Runnable.Web.RazorDocs` provides a Razor-powered documentation experience that can aggregate Markdown and C# API documentation into a browsable web UI. It is built on top of `ForgeTrust.Runnable.Web`, `ForgeTrust.Runnable.Web.RazorWire`, and the Runnable module system.

## Features

- **Markdown harvesting**: Collects and renders Markdown documentation pages.
- **C# API harvesting**: Extracts structured API documentation from C# source.
- **Aggregated docs model**: Combines multiple sources into a unified navigation tree.
- **Web UI**: Ships controllers, views, and sidebar components for browsing docs.
- **Tailwind integration**: Uses `ForgeTrust.Runnable.Web.Tailwind` for generated styling.

## Key Types

- **`RazorDocsWebModule`**: Main Runnable web module for wiring the docs experience into an application.
- **`DocAggregator`**: Coordinates collection and shaping of documentation content.
- **`MarkdownHarvester`**: Turns Markdown sources into docs pages.
- **`CSharpDocHarvester`**: Extracts API documentation from C# source.
- **`DocsController`**: Serves documentation pages in the web app.

## Usage

Reference the package and add the module to your Runnable web application:

```csharp
await WebApp<RazorDocsWebModule>.RunAsync(args);
```

This package is a good fit when you want an internal or product-facing documentation site that lives inside the same Runnable ecosystem as the rest of your web application.

## Notes

- `ForgeTrust.Runnable.Web.RazorDocs` is a higher-level package than the core web primitives and is intended for documentation experiences specifically.
- It depends on the Tailwind package family for styling and the caching package for docs aggregation performance.
