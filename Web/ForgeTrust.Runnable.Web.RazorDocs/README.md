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

## Landing Curation

RazorDocs can turn the root docs landing into a curated proof-path surface by reading `featured_pages` from the repository-root `README.md` metadata.

### Authoring contract

`featured_pages` is parsed as part of `DocMetadata`, so the metadata contract stays page-agnostic. The built-in `/docs` landing consumes those entries only from the root `README.md` metadata, but authors can now supply that metadata in either of two places:

- Inline Markdown front matter at the top of the `.md` file
- A paired sidecar YAML file such as `README.md.yml` or `README.md.yaml`

Inline front matter remains the default authoring path for ordinary docs pages. Paired sidecars are the recommended escape hatch for portability-sensitive files such as `README.md`, where raw front matter renders poorly on GitHub and other plain Markdown surfaces.

```yaml
# README.md.yml
title: Runnable
summary: Follow the proof paths that explain what this framework is for and how it composes.
featured_pages:
  - question: How does composition work?
    path: guides/composition.md
    supporting_copy: Start with the composition guide before drilling into APIs.
    order: 10
  - question: Show me an end-to-end example
    path: examples/hello-world/README.md
    order: 20
```

### Field behavior

- `question` is the reader-facing label shown on the landing card. If omitted, RazorDocs falls back to the destination page title.
- `path` accepts either the source path or canonical docs path for the destination page. RazorDocs normalizes forward-slash and backslash separators during resolution.
- `supporting_copy` is optional landing-only text. If omitted, RazorDocs falls back to the destination page summary.
- `order` is optional. Lower values sort first, and ties preserve authored order.

### Fallback and visibility rules

- If the root `README.md` is missing, the landing stays on the neutral docs index.
- If `featured_pages` is missing or empty, the landing stays on the neutral docs index.
- If both `README.md.yml` and `README.md.yaml` exist for the same Markdown file, RazorDocs logs a warning and ignores both sidecars until the conflict is removed.
- If both sidecar metadata and inline front matter define the same field, inline front matter wins and the sidecar acts as fallback metadata only.
- Invalid sidecar YAML logs a warning and falls back to the inline/default metadata path instead of breaking the page harvest.
- If a featured path is missing, hidden from public navigation, or duplicated, RazorDocs skips it and logs a warning.
- If all featured entries are skipped, RazorDocs falls back to the neutral docs index instead of rendering broken cards.

### Pitfalls

- Do not create both `.yml` and `.yaml` sidecars for the same Markdown file. RazorDocs treats that as an authoring error and ignores both.
- Do not use a sidecar as a second secret metadata system. It supports the same `DocMetadata` schema as inline front matter, and it is best reserved for files whose Markdown needs to stay portable on other surfaces.
- README portability matters most at the repository and package level. In this repo, authored `README.md` files should stay free of inline front matter so GitHub renders them cleanly.

## Metadata-Driven Page Type Display

RazorDocs treats `page_type` metadata as structured UI input, not just as opaque search metadata. The built-in landing cards, detail pages, and search results all normalize the same metadata through `DocMetadataPresentation.ResolvePageTypeBadge()`.

### Built-in normalization

- Known values such as `guide`, `example`, `api-reference`, `internals`, `how-to`, `start-here`, `troubleshooting`, `glossary`, and `faq` render with stable labels and intentional badge variants.
- Unknown values still render safely: RazorDocs normalizes whitespace, underscores, and dashes, then falls back to a neutral title-cased badge.
- Missing or blank `page_type` values render no badge at all instead of leaving empty chrome behind.

### Search payload contract

The `/docs/search-index.json` payload continues to emit the raw `pageType` metadata value and now also includes:

- `pageTypeLabel` for the normalized display label used by the built-in search UI
- `pageTypeVariant` for the built-in badge variant suffix used by CSS classes such as `docs-page-badge--guide`

These fields let custom search clients stay visually aligned with the landing and detail experiences without re-implementing the mapping table.

## Related Projects

- [ForgeTrust.Runnable.Web.RazorDocs.Standalone](../ForgeTrust.Runnable.Web.RazorDocs.Standalone/README.md) for the runnable/exportable host used in docs export and smoke testing
- [Back to Web List](../README.md)
- [Back to Root](../../README.md)

## Notes

- This package is the reusable documentation surface; `ForgeTrust.Runnable.Web.RazorDocs.Standalone` is the thin executable wrapper used for local hosting and export scenarios.
- The bundled RazorDocs UI already includes its generated stylesheet as a static web asset. The layout resolves the correct stylesheet path automatically from the host's root module shape for standalone/root-module hosts versus embedded application-part consumers.
- Consumers do not need to call `services.AddTailwind()` unless they also want Tailwind build/watch integration for their own host application's CSS.
- It depends on the Tailwind package family for RazorDocs package build-time styling generation and on the caching package for docs aggregation performance.
