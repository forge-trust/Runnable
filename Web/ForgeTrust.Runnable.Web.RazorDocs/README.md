# ForgeTrust.Runnable.Web.RazorDocs

Documentation site generation and hosting for Runnable web applications.

## Overview

`ForgeTrust.Runnable.Web.RazorDocs` is the reusable Razor Class Library package behind the RazorDocs experience. It aggregates Markdown and C# API documentation into a browsable `/docs` UI and is intended to be embedded into Runnable web applications or used by the standalone RazorDocs host.

## What It Provides

- `RazorDocsWebModule` for wiring the docs UI into a Runnable web host
- `AddRazorDocs()` for typed options binding and core service registration
- `DocAggregator` plus the built-in Markdown and C# harvesters
- Search UI assets and the `/docs` MVC surface used by RazorDocs consumers
- Structured trust metadata plus a built-in trust bar for release notes, upgrade guides, and other pages that need status and provenance near the top
- Precompiled Tailwind-powered styling with layout-time path resolution for root-module and embedded hosts

## Styling Boundary

When choosing where a new RazorDocs style should live, use this order:

1. If the surface needs a reusable component contract or a selector shared across CSS and JavaScript, use a semantic class.
2. Otherwise, if RazorDocs does not fully control the nested content markup, use wrapper-scoped semantic CSS.
3. Otherwise, for one-off package chrome that RazorDocs owns directly, prefer Tailwind utility classes in markup.

This section is the normative source of truth for the boundary. `DESIGN.md` explains why the rule exists and how to review edge cases. `ROADMAP.md` only points future work back to this contract.

### Decision Matrix

| Surface | Default | Why | Real examples | Exception / note |
| --- | --- | --- | --- | --- |
| One-off owned package chrome in Razor views | Prefer Tailwind utility classes in markup | RazorDocs fully owns the markup, so local utility classes keep intent obvious where the change happens | docs landing shell in `Views/Docs/Index.cshtml`, sidebar shell and layout framing in `Views/Shared/_Layout.cshtml`, one-off page header spacing in `Views/Docs/Details.cshtml` | If the same styling contract repeats across package surfaces, promote it to a semantic component class instead of copying long utility strings |
| Reusable owned package components or stable cross-file UI selectors | Use semantic component classes | Shared selectors keep repeated UI stable across Razor, CSS, and sometimes JavaScript | `docs-page-badge`, `docs-metadata-chip`, `docs-search-page`, `docs-search-page-filters-toggle`, `docs-search-page-active-filters` | Utilities can still handle surrounding layout and one-off placement |
| Harvested or generated document bodies that RazorDocs does not fully author element by element | Use wrapper-scoped semantic CSS such as `.docs-content ...` | RazorDocs cannot safely push utility classes into nested harvested HTML | headings, paragraphs, code blocks, overload groups, and namespace sections inside `.docs-content` in `Views/Docs/Details.cshtml` and `wwwroot/css/app.css` | Do not rewrite harvested nested HTML just to satisfy utility-class purity |
| JavaScript-generated or stateful UI that needs CSS and JavaScript to share stable hooks | Use semantic hook classes, then style them in CSS | Runtime UI needs stable names both the stylesheet and script can rely on | search result rows, filter chips, active-filter pills, and state containers in `wwwroot/docs/search.css` and `wwwroot/docs/search-client.js` | Use `id` values where uniqueness or ARIA wiring require them, but keep reusable styling and state contracts on semantic classes |

### Common Calls

- New one-off page header spacing or typography in owned Razor markup: use Tailwind utilities in the view.
- New reusable badge, metadata chip, or shared search workspace shell element: add or extend a semantic component class, then use utilities around it only when they are purely local.
- For `Views/Docs/Search.cshtml`, keep the stateful search container or interactive hook semantic, but use local utilities for one-off header copy, helper layout, and fallback-link chrome inside that view.
- Restyling paragraphs, headings, or code blocks inside `.docs-content`: update wrapper-scoped CSS instead of pushing utility classes into harvested HTML.
- New search filter pill, active-filter surface, or other stateful search UI: use a semantic hook class because CSS and JavaScript both need to recognize it.

### Terms

- **Package chrome**: one-off layout and presentation markup that RazorDocs owns directly, such as page shells, spacing, and framing.
- **Harvested content**: nested documentation HTML that RazorDocs renders but does not fully author element by element, such as the body inside `.docs-content`.
- **Stable selector / hook**: a semantic class or required unique `id` that Razor, CSS, accessibility wiring, and sometimes JavaScript rely on consistently across files.

### Pitfalls

- Do not refactor between utilities and semantic CSS for purity alone. Follow the surface contract unless a real usability or maintainability problem exists.
- Do not treat required `id` values, such as `docs-search-page-input` or `docs-search-page-filters-panel`, as the reusable styling contract. They exist for uniqueness, targeting, and ARIA relationships.
- Do not assume every child inside a semantic search container needs its own semantic class; local typography and spacing inside one view can still stay inline.
- Do not add semantic classes to static package chrome when plain utilities are clearer and the styling is truly local.

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
- Only canonical slugs are served directly; label- or alias-shaped section requests redirect to the canonical section route.
- When a section has an authored landing doc, RazorDocs redirects the section route to that page.
- Sections with visible pages but no landing doc render a grouped fallback section page instead of a dead end.
- Invalid slugs or sections with no public pages render an unavailable section surface with recovery links back to `/docs` and `Start Here`.

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

## Metadata-Driven Wayfinding

RazorDocs can render two kinds of page-local wayfinding on details pages without scraping rendered HTML after the fact:

- `On this page` links come from the harvested `DocNode.Outline` contract.
- `Previous` and `Next` proof-path links come from explicit metadata, not folder inference.

### Sequence contract

Use `sequence_key` together with `order` when a set of pages should behave like one proof path:

```yaml
sequence_key: razorwire-proof
order: 20
related_pages:
  - Web/ForgeTrust.Runnable.Web.RazorWire/README.md
  - Web/ForgeTrust.Runnable.Web.RazorWire/Docs/antiforgery.md
```

- `sequence_key` opts a page into a specific sequence. Pages do not join a sequence just because they share a folder.
- `order` determines the relative previous/next position inside that sequence.
- `related_pages` stays independent from sequencing and can point to source paths, canonical docs paths, or exact page titles.
- RazorDocs publishes authored sequence metadata to the generated `/docs/search-index.json` file for custom clients and integrations. The `sequence_key` front-matter value becomes `sequenceKey`, `order` stays `order`, and `related_pages` stays separate as `relatedPages`; for example: `{ "sequenceKey": "razorwire-proof", "order": 20, "relatedPages": ["Web/ForgeTrust.Runnable.Web.RazorWire/README.md"] }`.

### Resolution rules

- Previous/next links render only when the current page has both `sequence_key` and `order`.
- RazorDocs only sequences navigable pages. Fragment-only anchor stubs and pages hidden from public navigation do not appear in proof-path navigation.
- Related pages are deduplicated against the current page and any resolved previous/next neighbors.

### Pitfalls

- Do not rely on filename prefixes or folder adjacency for proof-path behavior in this slice. Use explicit `sequence_key` values instead.
- Do not expect `related_pages` to imply ordering. Related links stay unordered beyond the authored list order.

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

## Custom Harvester Outline Contract

The built-in Markdown and C# harvesters now populate `DocNode.Outline` directly during harvest. Custom `IDocHarvester` implementations should do the same when they want:

- `On this page` links on details views
- heading metadata in `/docs/search-index.json`
- stable behavior without re-parsing rendered HTML later

Each outline entry should provide the rendered fragment `Id`, the reader-facing `Title`, and the normalized heading `Level`. For visual parity with the built-in wayfinding UI, custom `IDocHarvester` implementations should populate `DocNode.Outline` only with entries that have a non-empty rendered fragment `Id` and non-empty `Title`; headings or generated sections missing either value are skipped by the built-ins. The Markdown harvester emits source-ordered H2-H3 headings by default, with titles normalized from inline heading text and IDs taken from the rendered heading fragment. The C# harvester emits level 2 entries for documented types and enums, and level 3 entries for method groups and properties. Matching those defaults keeps custom outlines aligned with the built-in `On this page` section and search heading metadata.

Public visibility note:

- `HideFromSearch = true` removes a page from the search payload directly.
- `HideFromPublicNav = true` also removes the page from the search payload because the public shell treats hidden pages as fully non-public.

## Trust Metadata For Release Notes And Policy Pages

RazorDocs can also render a top-of-page trust bar from nested `trust` metadata. Runnable uses this for its own release notes, upgrade policy, and changelog pages so the product doubles as a working example for consumers.

```yaml
trust:
  status: Unreleased
  summary: This page is provisional until the next tag is cut.
  freshness: Updated as changes land on main.
  change_scope: Repository-wide.
  migration:
    label: Read the upgrade policy
    href: /docs/releases/upgrade-policy.md.html
  archive: Tagged release notes will keep the final narrative once the version ships.
  sources:
    - CHANGELOG.md
    - releases/unreleased.md
```

### Field behavior

- `status` is the compact top-level state, such as `Unreleased` or `Pre-1.0 policy`.
- `summary` is the short trust statement shown beside the status.
- `freshness` explains how current the page is and how stable readers should assume it is.
- `change_scope` calls out which surfaces the note covers.
- `migration` is an optional label plus browser-facing `href` to the adoption guidance.
- `archive` explains where the durable tagged record or long-term home lives.
- `sources` is an optional list of provenance notes or upstream artifacts.

### Merge behavior

- Inline front matter and sidecar YAML both use the same nested `trust` schema.
- Inline metadata wins over sidecar metadata field by field.
- Explicit empty lists such as `sources: []` are authoritative and suppress fallback lists.

### Pitfalls

- Use a browser-facing `href` for `migration`, not a source path, because the trust bar renders a plain link without path rewriting.
- Keep private maintainer-only runbooks outside harvested docs. Hidden pages are removed from nav and search, but they are still public if linked directly.
- Do not turn the trust bar into marketing chrome. It should answer status, safety, and provenance questions quickly.

## Related Projects

- [ForgeTrust.Runnable.Web.RazorDocs.Standalone](../ForgeTrust.Runnable.Web.RazorDocs.Standalone/README.md) for the runnable/exportable host used in docs export and smoke testing
- [Back to Web List](../README.md)
- [Back to Root](../../README.md)

## Notes

- This package is the reusable documentation surface; `ForgeTrust.Runnable.Web.RazorDocs.Standalone` is the thin executable wrapper used for local hosting and export scenarios.
- The bundled RazorDocs UI already includes its generated stylesheet as a static web asset. The layout resolves the correct stylesheet path automatically from the host's root module shape for standalone/root-module hosts versus embedded application-part consumers.
- Consumers do not need to call `services.AddTailwind()` unless they also want Tailwind build/watch integration for their own host application's CSS.
- It depends on the Tailwind package family for RazorDocs package build-time styling generation and on the caching package for docs aggregation performance.
