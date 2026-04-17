# RazorDocs Roadmap

This note keeps the in-repo RazorDocs plan aligned with the phased GitHub roadmap.

## Product Direction

`RazorDocs` is being shaped as a `.NET`-native documentation framework for libraries and frameworks that unifies authored guides, examples, and source-derived API reference in one experience for human and agentic readers.

`Runnable` is the first flagship consumer of this work. The initial phases should make Runnable's documentation more trustworthy for evaluators while also proving the reusable RazorDocs product surface.

## Phase 1: Metadata Foundation

Status: shipped

Phase 1 focused on the shared metadata and indexing layer:

- front matter and structured doc metadata
- metadata defaults for markdown and harvested API docs
- namespace README merging
- metadata-backed search payloads
- metadata-aware navigation and breadcrumb behavior

This phase creates the foundation for later landing-page, search, and wayfinding work without hardcoding repo-specific behavior into views.

## Phase 2: Evaluator Experience

GitHub milestone: [RazorDocs Phase 2: Evaluator Experience](https://github.com/forge-trust/Runnable/milestone/1)

Phase 2 is the first visible product slice built on top of the metadata foundation. The goal is to help an evaluating engineer answer trust questions quickly and move from high-level orientation into concrete examples and API details.

Primary issues:

- [#101](https://github.com/forge-trust/Runnable/issues/101) Rebuild public docs navigation around user intent
- [#106](https://github.com/forge-trust/Runnable/issues/106) Redesign docs search UI with filters and richer results
- [#107](https://github.com/forge-trust/Runnable/issues/107) Create Start Here, troubleshooting, and glossary content foundations
- [#109](https://github.com/forge-trust/Runnable/issues/109) Add deep-page wayfinding with TOC, related links, and next steps
- [#123](https://github.com/forge-trust/Runnable/issues/123) Add evaluator-first docs landing with featured proof paths
- [#124](https://github.com/forge-trust/Runnable/issues/124) Render metadata-driven page type badges across landing, details, and search
- [#125](https://github.com/forge-trust/Runnable/issues/125) Add featured page curation for evaluator questions
- [#126](https://github.com/forge-trust/Runnable/issues/126) Add a concise RazorDocs product explainer to the flagship docs experience

Recommended shipping emphasis:

1. featured page curation
2. evaluator-first landing
3. page-type badges
4. concise RazorDocs product explainer
5. deeper content and wayfinding follow-through

## Phase 3: Routing, Relevance, and Telemetry

GitHub milestone: [RazorDocs Phase 3: Routing, Relevance, and Telemetry](https://github.com/forge-trust/Runnable/milestone/2)

Phase 3 deepens the intelligence layer after the evaluator-facing experience is visible. The goal is to improve URL quality, search relevance, and the feedback loop for measuring whether docs findability is improving.

Primary issues:

- [#102](https://github.com/forge-trust/Runnable/issues/102) Add canonical doc slugs with legacy-safe redirects
- [#104](https://github.com/forge-trust/Runnable/issues/104) Expand search index with metadata and visibility controls
- [#105](https://github.com/forge-trust/Runnable/issues/105) Tune search ranking for intent-based relevance
- [#108](https://github.com/forge-trust/Runnable/issues/108) Add config-gated search telemetry and docs metrics

## Notes

- These phases are intended to be additive. Phase 2 should consume the Phase 1 foundation rather than reworking it.
- `Runnable` remains the first proof site, but the roadmap should favor reusable RazorDocs capabilities over one-off customizations.
- Future phases can expand into more explicit agent-facing features once the evaluator experience and search relevance are working well.
