# ForgeTrust.Runnable.Web.RazorDocs

Reusable Razor Class Library package for harvesting and serving repository documentation inside a Runnable web application.

## What it provides

- `RazorDocsWebModule` for wiring the docs UI into a Runnable web host
- `AddRazorDocs()` for typed options binding and core service registration
- `DocAggregator` plus the built-in markdown and C# harvesters
- Search UI assets and the `/docs` MVC surface used by RazorDocs consumers

## Configuration

Slice 1 supports source-backed docs via `RazorDocsOptions`:

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

If `RazorDocs:Source:RepositoryRoot` is omitted, the package falls back to repository discovery from the app content root. Bundle mode is modeled but intentionally rejected until Slice 2 lands.

## Public Sections

RazorDocs now organizes public documentation around a fixed section-first model instead of a flat directory-first landing.

### Built-in sections

- `Start Here`
- `Concepts`
- `How-to Guides`
- `Examples`
- `API Reference`
- `Troubleshooting`
- `Internals`

These sections back the `/docs` home, the sidebar shell, and the dedicated section routes under `/docs/sections/{slug}`.

### `nav_group` normalization and fallback rules

- `nav_group` can explicitly select a built-in public section by canonical label, slug, or alias.
- Invalid explicit `nav_group` values log a warning and fall back to RazorDocs-derived section assignment instead of creating ad hoc groups.
- Markdown docs with no explicit `nav_group` are derived into built-in sections using path and filename heuristics:
  - repository-root `README.md` and start-like names such as `quickstart` or `getting-started` fall into `Start Here`
  - `examples/` content falls into `Examples`
  - concepts, architecture, explanation, and glossary-style paths fall into `Concepts`
  - troubleshooting, faq, debug, and error-oriented paths fall into `Troubleshooting`
  - internal-oriented paths fall into `Internals`
  - anything else falls into `How-to Guides`
- API reference content continues to use the canonical `API Reference` section.

### Section routes and landing docs

- `/docs/sections/{slug}` resolves one public section route such as `/docs/sections/start-here` or `/docs/sections/api-reference`.
- If the section has an authored landing doc, RazorDocs redirects the section route to that page.
- If the section has visible pages but no landing doc, RazorDocs renders a grouped fallback section page instead of a dead end.
- If the slug is invalid or the section has no public pages, RazorDocs renders an unavailable section surface with recovery links back to `/docs` and `Start Here`.

### `section_landing`

Use `section_landing: true` on a page to mark it as the authored entry point for its public section.

```yaml
title: Start Here
nav_group: Start Here
section_landing: true
summary: Start with the strongest evaluator proof path before drilling into implementation detail.
```

Field behavior and pitfalls:

- The page must still belong to a valid built-in public section through explicit or derived `nav_group`.
- If multiple docs in one section set `section_landing: true`, RazorDocs keeps the lowest `order` value, then the lowest canonical path, and logs a warning for the others.
- A section landing doc can also author `featured_pages`; RazorDocs uses those entries for section-level “next steps” on the detail page and for the section preview links surfaced on `/docs`.
- `HideFromPublicNav = true` always wins. Hidden pages do not appear in section routes, the sidebar, the docs home, or the public search index even if they declare a section or landing status.

## Landing Curation

RazorDocs can turn the root docs landing into a curated proof-path surface by reading `featured_pages` from the repository-root `README.md` metadata.

### Authoring contract

`featured_pages` is parsed as part of `DocMetadata`, so the metadata contract stays page-agnostic. RazorDocs uses those entries in two places:

- the root `README.md` metadata drives the primary proof-path rows on `/docs`
- any authored section landing doc can drive its own section-level next-step rows and the section preview links shown on `/docs`

Authors can now supply that metadata in either of two places:

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
- `publicSection` for the normalized built-in section slug when the page is publicly visible
- `publicSectionLabel` for the reader-facing section label
- `isSectionLanding` for authored section landing entry points

These fields let custom search clients stay visually aligned with the landing and detail experiences without re-implementing the mapping table.

Public visibility note:

- `HideFromSearch = true` removes a page from the search payload directly.
- `HideFromPublicNav = true` also removes the page from the search payload because the public shell treats hidden pages as fully non-public.

## Related Projects

- [ForgeTrust.Runnable.Web.RazorDocs.Standalone](../ForgeTrust.Runnable.Web.RazorDocs.Standalone/README.md) for the runnable/exportable host used in docs export and smoke testing
- [Back to Web List](../README.md)
- [Back to Root](../../README.md)
