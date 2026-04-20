# RazorDocs Design Language

## Purpose

RazorDocs should feel like a focused documentation workspace, not a marketing site and not a generic SaaS dashboard. The UI should help people orient quickly, scan densely, and move deeper into docs with very little friction.

## Core Tone

- Quietly technical
- Editorial rather than card-heavy
- Dense, but never cramped
- Confident without looking flashy

If a new surface starts to feel like a feature grid, a landing page, or an AI-generated admin template, pull it back.

## Visual System

### Typography

- Primary typeface: `Outfit`
- Body copy should stay clean and readable with generous line-height
- Headings should feel compact and intentional, not oversized hero copy
- Search results should privilege readable title hierarchy over decorative framing

### Color

- Base surfaces: dark slate family
- Accent: cyan for focus, active state, and high-value calls to action
- Borders and separators should do most of the structure work
- Avoid adding extra accent colors unless the feature truly needs semantic differentiation

### Surfaces

- Prefer layered panels, separators, and subtle fills over heavy boxed cards
- Result lists should read like an editorial index with strong rows, not isolated product cards
- Keep shadows minimal; contrast and spacing should carry the layout

## Search Workspace

`/docs/search` is a search-first workspace.

- Keep the primary search input visually dominant
- Place filters directly under or adjacent to the query area
- Keep one main results stream, ranked by relevance
- Use badges and breadcrumbs to add context without fragmenting the scan path

### Starter State

The empty state should guide without feeling promotional.

- Include a one-sentence orientation
- Show clickable suggestion chips
- Explain that filters can also be used for browse mode before typing

### Results

Results should be a high-information list.

- Title is the strongest element
- Breadcrumbs come first and stay subtle
- Badges are compact metadata, not visual decoration
- Snippets should stay short and readable
- Highlight matches with restrained `<mark>` styling

### Filters

- Desktop: filters are visible within the page workspace
- Mobile: collapse filters behind a compact toggle with active-filter summary pills
- Disabled zero-result options should remain visible so the dataset shape stays legible

## State Design

Search has distinct states and they should look distinct.

- First load: show skeleton rows and clear “Loading search index...” messaging
- Refinement updates: keep prior results visible and use a subtle busy state
- No results: explain that the search worked, then offer recovery paths
- Failure: explain that search itself is unavailable, show retry, and provide fallback links

Do not reuse the no-results treatment for actual failures.

## Interaction Rules

- `/` focuses the visible search input when the user is not already typing
- `Cmd/Ctrl+K` opens the search workspace or focuses the advanced input
- Browser history should feel natural:
  - typing updates URL state with replace semantics
  - deliberate filter changes create navigable history entries
- Back/Forward must restore query, filters, and rendered results

## Accessibility

- Preserve semantic headings, labels, and live regions for status changes
- Loading, failure, and no-results states must remain understandable without visual context
- Focus should move predictably for shortcuts, retry, and suggestion chips
- Mobile layouts should keep search and results visible without forcing long filter scrolls

## Anti-Patterns

Avoid these by default:

- chunky boxed result cards
- oversized marketing hero treatments
- decorative gradients inside core reading surfaces
- multiple competing result columns
- hiding the corpus shape behind over-minimal filter UI
- introducing visual styles that do not already fit the dark-slate plus cyan system
