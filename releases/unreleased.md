# Unreleased

This is the living release note for the next coordinated Runnable version. It is intentionally written in the same blog-style shape we want future tagged releases to use, but everything here remains provisional until a tag is cut.

## What is taking shape

Runnable is putting the release contract in place before `v0.1.0`. This slice is about making release notes auditable, public, and reusable:

- a root changelog that acts as the compact ledger
- a public unreleased proof artifact
- a pre-1.0 upgrade policy with a clear migration home
- a top-of-page trust bar for release notes and policy pages
- pull-request guards that keep PR titles and unreleased entries aligned with future release automation

## Included in the next coordinated version

### Release and docs surface

- The `/docs` landing now promotes a Releases entry point alongside product proof paths.
- Runnable now ships a public release hub, a changelog, an unreleased page, and a tagged release template inside the repository.
- Release-note pages can show status, freshness, scope, migration guidance, and provenance in a shared trust bar instead of bespoke page chrome.

### Contribution contract

- Pull request titles are now expected to follow Conventional Commits so the merge history is machine-readable for future automation.
- Pull requests are expected to update this page unless maintainers explicitly mark the change as outside the public release story.
- Markdown-only changes on `main` now republish the docs surface, so release-note and policy edits are treated as first-class product updates.

### Console and CLI polish

- Runnable console apps can now opt into a command-first output contract so public CLI help and validation flows stay quiet instead of printing Generic Host lifecycle chatter.
- RazorWire CLI now uses that contract for `--help`, `export --help`, invalid option output, and missing-source validation while still preserving command-owned export progress logs.
- The shared console startup seam now exposes `ConsoleOptions` and `ConsoleOutputMode`, so future public Runnable CLIs can adopt the same behavior without forking startup logic.

### RazorDocs product example

- Runnable's own release pages now double as a working RazorDocs example for consumers who want better release notes.
- Public docs navigation now groups pages by intent-first sections, preserves authored editorial breadcrumbs, and keeps Start Here recovery links hidden when that section is unavailable.
- The release contract is designed so future tooling can generate both a changelog entry and a blog-style tagged release note from the same underlying signals.
- RazorDocs now rewrites authored doc links from a harvested target manifest instead of broad suffix heuristics, so normal site links such as `../privacy.html` stay untouched and missing doc targets do not become broken `/docs/...` routes.

## Migration watch

There is no tagged migration guide yet because Runnable has not cut `v0.1.0`. Until then:

- breaking changes should be called out here as soon as they land
- the stable policy lives in [Pre-1.0 upgrade policy](./upgrade-policy.md)
- finalized migration steps move into the tagged release note when the version ships

## Proof artifacts

- [Release hub](./README.md)
- [Changelog](../CHANGELOG.md)
- [Pre-1.0 upgrade policy](./upgrade-policy.md)
- [Release authoring checklist](./release-authoring-checklist.md)

## Before the first tag

The current intent is that everything already in this repository can be part of `v0.1.0` when Runnable is ready to release. This page is where that pile becomes visible and reviewable before the tag exists.
