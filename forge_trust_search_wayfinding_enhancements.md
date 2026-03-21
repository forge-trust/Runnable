# Enhancements for Improving Search & Wayfinding

**Recommendations for forge-trust.com / RazorDocs**

Focus: make search, navigation, and content structure work together so readers can find the right page without first learning the repository layout.

## Document details

- **Prepared for:** ForgeTrust
- **Scope:** Public documentation experience for search and wayfinding
- **Basis:** Review of the current forge-trust.com / RazorDocs site structure and representative content pages
- **Date:** March 15, 2026

## Top five actions to prioritize

1. Rebuild the top-level navigation around user intent: **Start Here, Concepts, How-to Guides, Examples, API Reference, Troubleshooting, and Internals**.
2. Upgrade search results with **filters, snippets, breadcrumbs, badges, and tuned ranking** so broad task queries surface guides before tests or raw namespaces.
3. Normalize **titles, slugs, and breadcrumbs** so pages use human-readable names instead of file names and duplicate headings.
4. Create a **metadata layer** for page type, audience, component, aliases, keywords, status, and ordering so search and navigation share the same source of truth.
5. Add **local wayfinding** on every deep page: section-aware sidebar, on-page table of contents, related links, and clear next steps.

---

## Executive summary

The documentation already contains strong material, especially the RazorWire overview, the anti-forgery guidance, and the MVC example. The main findability problem is not lack of content; it is that the site currently feels **repository-shaped instead of reader-shaped**. Tests, benchmarks, examples, namespaces, and guides compete for equal prominence, so users must understand your source tree before they can understand your product.

The highest-leverage change is to make **search and navigation intent-based**. A new reader asking broad questions such as “how do I get started?”, “how do forms work?”, or “how do I build an MVC example?” should reach curated guides and examples first. An experienced user looking for a specific symbol or namespace should still be able to reach reference pages quickly. That requires clearer information architecture, richer result design, and a metadata model that both navigation and search can use.

In practical terms, this means separating public-facing guidance from internal or contributor-oriented content, enriching the search experience beyond a single input field, and adding local wayfinding so no page becomes a dead end. The recommendations below are organized to produce quick wins first and then build toward durable search quality improvements.

## What success looks like

- A first-time visitor can reach a getting-started page or key guide in one click from the home page.
- Broad searches return guides, examples, and troubleshooting pages before tests, benchmarks, or internal artifacts.
- Exact symbol searches still resolve to API reference pages without friction.
- Deep pages always provide context, related links, and a next step.
- The docs team can measure findability through query analytics instead of relying on anecdotal feedback alone.

---

## Observed friction points on the current site

| Observation | Why it matters |
|---|---|
| The main navigation and landing experience expose tests, benchmarks, examples, namespaces, and guides together. | Users receive very little help distinguishing learning paths from internal or lower-priority content. |
| The search page is minimal and does not visibly offer filters, snippets, badges, or guidance for refining queries. | Search feels like an index box rather than a discovery workflow. |
| Several pages surface file names and duplicate headings such as `README.md`, `antiforgery`, `benchmarks`, or repeated package names. | The site reads as generated output rather than a curated documentation product, which weakens scannability and trust. |
| Some navigation labels are duplicated or ambiguous, including repeated entries such as `NullView` and `RawHtmlStreamAction`. | Readers cannot easily distinguish which item is the right destination, especially inside dense reference trees. |
| Good task-oriented pages exist, but the site does not consistently steer readers toward them first. | Valuable content is present, but users must discover it by exploring rather than being intentionally guided to it. |

---

## Design principles for search and wayfinding

- **Intent first:** organize around what the reader is trying to do, not around the repository or namespace tree.
- **Progressive disclosure:** show the most useful public paths first and reveal internal depth only when relevant.
- **One concept, one label:** use one clear title for each concept and avoid file-name leakage or duplicate labels.
- **Shared metadata:** search, navigation, breadcrumbs, and related links should all be driven by the same page metadata model.
- **No dead ends:** every page should explain where the reader is, what the page type is, and what to read next.

---

## Priority actions

| Theme | Enhancement | Impact | Priority |
|---|---|---|---|
| Information architecture | Create a public-facing top-level structure and demote tests, benchmarks, and contributor content from default navigation. | High | Now |
| Search results UX | Add filters, snippets, breadcrumbs, badges, keyboard access, and strong no-results guidance. | High | Now |
| Metadata layer | Add page type, audience, aliases, keywords, status, ordering, and search visibility fields. | High | Next |
| Label cleanup | Normalize titles, slugs, breadcrumbs, and duplicate navigation labels. | High | Now |
| Local wayfinding | Add on-page tables of contents, related links, previous/next links, and section context. | Medium | Next |
| Analytics and tuning | Track query behavior, no-result terms, refinements, and success clicks to guide ranking changes. | Medium | Next |

---

## 1. Rebuild the top-level information architecture

The current site should present a documentation model, not a source tree. The default public navigation should be simplified so readers can choose a path based on intent and audience, while internal or generated content remains available without dominating the experience.

### Recommended changes

- Introduce a public top-level structure: **Start Here, Concepts, How-to Guides, Examples, API Reference, Troubleshooting, and Internals**.
- Move tests, benchmarks, and contributor-only material into **Internals** or hide them from the default public sidebar.
- Add section landing pages that explain what belongs in each section and who it is for.
- Create audience-specific entry points such as **New to Runnable**, **Building with RazorWire**, and **Contributing to the project**.
- Ensure the home page summarizes the product and links directly to the most common journeys rather than opening with repository status details.

### Recommended public navigation model

- **Start Here** — quick introduction, installation, first example, and a recommended reading path.
- **Concepts** — core ideas such as modules, islands, streams, forms, and anti-forgery.
- **How-to Guides** — task-based pages for common implementation work.
- **Examples** — guided walkthroughs for MVC, web apps, console apps, and sample integrations.
- **API Reference** — namespaces, types, methods, and symbols for exact lookup.
- **Troubleshooting** — fixes for common problems, including search-friendly symptom pages.
- **Internals** — benchmarks, tests, implementation notes, and contributor-focused material.

---

## 2. Upgrade search into a discovery workflow

Search should do more than match words. It should help readers interpret what they found, choose the right page type, and recover quickly when they searched with the wrong term. The search interface and ranking model should work together.

### Recommended changes

- Make search accessible globally with a clear input in the header and keyboard shortcuts such as `/` or `Cmd/Ctrl+K`.
- Return result cards that show **title, breadcrumb, summary snippet, page-type badge, and component/package badge**.
- Add filters for **page type, component/package, audience, and content status** so users can narrow results without rewriting their query.
- Provide autosuggestions that blend **popular pages, aliases, and exact API symbols**.
- Offer a strong no-results state with suggested alternative terms, nearby results, and recommended landing pages.
- Persist the query and selected filters in the URL so results can be shared and indexed.

### Recommended result card structure

Each result should include:

- Clear title
- Human-readable breadcrumb
- One-sentence summary
- Page-type badge such as **Guide**, **Example**, **API Reference**, or **Troubleshooting**
- Component badge such as **RazorWire** or **Runnable**
- Highlighted matched terms in title and snippet

### Recommended ranking behavior

| Signal | Example | Desired effect |
|---|---|---|
| Exact title or symbol match | `RazorWireBridge.Frame` or `Security & Anti-Forgery` | Very strong boost so precise queries land immediately. |
| Alias or synonym match | `csrf`, `anti forgery`, `turbo frames`, `sse` | Strong boost so common language maps to your canonical page titles. |
| Guide / example / troubleshooting page type | Broad task query such as `forms mvc example` | Boost for broad learning queries. |
| Heading match plus summary match | Term appears in heading and page summary | Moderate boost to reinforce curated pages. |
| Internal or contributor content | Tests, benchmarks, generated internals | Demote by default unless the user explicitly filters for internals. |

### Search UX enhancements worth adding early

- Recently viewed pages in the command palette or search overlay
- Popular searches for first-time users
- “Did you mean?” support for typos and terminology drift
- Section filters that can be toggled without leaving the result page
- Empty-state links to **Start Here** and **Troubleshooting**

---

## 3. Strengthen local wayfinding inside every page

Once a reader lands on a page, the site should help them orient quickly and continue without backtracking. That requires consistent page chrome and a small set of predictable navigation cues.

### Recommended changes

- Enforce **one H1 per page** and remove file names such as `README.md` or `antiforgery.md` from visible breadcrumbs and headings.
- Add a local **table of contents** for pages with substantial section depth.
- Show the **page type** prominently: Guide, Example, API Reference, Troubleshooting, or Internals.
- Include **related links** and **next-step suggestions** at the end of each page.
- Keep the sidebar aware of the current section so readers can see nearby siblings and parents.
- Provide **previous/next navigation** for curated guide sequences and example walkthroughs.

### Minimum wayfinding pattern for every deep page

Every substantial page should include:

1. A single human-readable page title
2. A short summary or purpose statement near the top
3. Breadcrumbs that use product language rather than file names
4. An on-page TOC when section depth is meaningful
5. Related pages or next steps near the end

---

## 4. Create a metadata and indexing foundation

Search quality and navigation quality will only stay good if they are both driven by structured metadata instead of fragile heuristics. Add front matter or build-time metadata for each page and make it the source of truth for ranking, filtering, grouping, and ordering.

### Recommended changes

- Store metadata alongside content so authors can control how pages appear in navigation and search.
- Support aliases and keywords so readers can search using everyday language instead of exact page titles.
- Let pages opt out of public navigation or public search without removing them from the site entirely.
- Use metadata to power section landing pages, related links, and package overviews.

### Example metadata model

```yaml
title: RazorWire MVC Example
summary: Build a reactive ASP.NET Core MVC app with RazorWire.
page_type: example
audience: implementer
component: RazorWire
aliases:
  - turbo streams mvc example
  - html over the wire example
keywords:
  - sse
  - islands
  - turbo frames
  - forms
status: preview
nav_group: Examples
order: 20
hide_from_public_nav: false
hide_from_search: false
related_pages:
  - Security & Anti-Forgery
  - RazorWire overview
```

### Why metadata matters

A shared metadata layer allows you to:

- Tune search without hardcoding page-specific ranking rules
- Build cleaner navigation automatically
- Generate related links with better relevance
- Create audience-based views without duplicating content
- Hide internals from novice users while keeping them available

---

## 5. Make content architecture support findability

Search and wayfinding improve fastest when the content set itself is organized into clear page types. The current site already has some strong examples of task-oriented documentation; those should become templates for the rest of the site.

### Recommended changes

- Create a true **Start Here** page with installation, first success path, and recommended next reads.
- Add task-oriented guides for high-friction topics such as **forms, anti-forgery, streaming, caching, and deployment constraints**.
- Add troubleshooting pages written around **symptoms and error language** so they are easy to discover via search.
- Rewrite example pages as walkthroughs with expected output, architecture overview, file map, and “adapt this to your app” guidance.
- Use API reference pages for exact lookup, but pair them with conceptual and how-to pages for broader learning intent.
- Add a glossary and a terminology map so synonyms resolve to canonical concepts.

### Suggested content templates by page type

**Guide**
- What this page helps you do
- When to use this approach
- Steps with copy/paste examples
- Common pitfalls
- Related docs

**Example**
- What you are building
- Architecture overview
- File map
- Run it locally
- Expected behavior
- Adaptation notes

**Troubleshooting**
- Symptom
- Likely cause
- Fix
- Prevention
- Related pages

**API Reference**
- What this type or namespace is for
- Common use cases
- Minimal example
- Key members
- Related conceptual docs

---

## Governance and measurement

After the first round of improvements, the site should be run as a measurable product. Search logs and navigation analytics will reveal where terminology gaps exist, which pages fail to convert searches into clicks, and which pages require better summaries or aliases.

| Metric | Why it matters |
|---|---|
| Search click-through rate | Shows whether results are compelling enough to produce a click. |
| No-results rate | Highlights missing content, poor aliases, or indexing gaps. |
| Search refinement rate | Indicates that users did not find what they needed on the first try. |
| Time to first meaningful click | Measures whether the information scent is strong and immediate. |
| Navigation depth to key guides | Reveals whether high-value pages are buried too deeply. |
| Bounce rate from search and landing pages | Shows where search or entry pages fail to orient the reader. |
| Helpfulness feedback on guide pages | Adds qualitative context to behavioral metrics. |

### Suggested operating cadence

- Review top search queries weekly
- Review no-result queries and refinements every sprint
- Track the top landing pages monthly
- Turn repeated support questions into docs backlog items
- Refresh titles, aliases, and summaries based on search telemetry

---

## Phased roadmap

### Phase 1: Cleanup and prioritization

- Hide or demote internal, test, and benchmark content from the default public navigation.
- Normalize titles, breadcrumbs, and slugs so file names and duplicate headings disappear.
- Introduce **Start Here**, section landing pages, and a clearer public top-level information architecture.
- Ship richer search results and add related links plus local TOCs.

### Phase 2: Metadata and ranking

- Add per-page metadata for type, audience, aliases, keywords, status, and ordering.
- Tune ranking rules so guides and examples surface ahead of internal content for broad queries.
- Create a synonym and alias dictionary for common user language.
- Instrument query analytics and review them regularly.

### Phase 3: Advanced discovery

- Introduce a command-palette style search experience for fast keyboard navigation.
- Recommend related pages based on shared metadata and reading paths.
- Promote high-value queries and content gaps into the documentation backlog.
- Use search telemetry to refine page summaries, titles, and aliases continuously.

---

## Suggested implementation rule

Do not improve search in isolation. Search quality will plateau quickly unless **page metadata, navigation hierarchy, and content page types** are improved at the same time.

---

## Appendix: Pages reviewed

The recommendations in this memo are grounded in a review of the public documentation experience on March 15, 2026, including these pages:

- Documentation Index: <https://forge-trust.com/>
- Search: <https://forge-trust.com/docs/search>
- Home / README: <https://forge-trust.com/docs/README.md.html>
- RazorWire overview: <https://forge-trust.com/docs/Namespaces/ForgeTrust.Runnable.Web.RazorWire.html>
- RazorWire MVC example: <https://forge-trust.com/docs/examples/razorwire-mvc/README.md.html>
- Security & Anti-Forgery: <https://forge-trust.com/docs/Web/ForgeTrust.Runnable.Web.RazorWire/Docs/antiforgery.md.html>
- Benchmarks: <https://forge-trust.com/docs/benchmarks/README.md.html>
