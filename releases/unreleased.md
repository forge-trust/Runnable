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
- Runnable now ships a generated package chooser that tells first-time adopters which package to install first, which optional modules to add next, and which proof paths to follow for release risk and working examples.

### Contribution contract

- Pull request titles are now expected to follow Conventional Commits so the merge history is machine-readable for future automation.
- Pull requests are expected to update this page unless maintainers explicitly mark the change as outside the public release story.
- Markdown-only changes on `main` now republish the docs surface, so release-note and policy edits are treated as first-class product updates.
- Runnable now exposes focused GitHub issue forms for bug reports and docs/developer-experience feedback, with the root README and contribution guide pointing developers to that feedback path.
- Public contribution surfaces now steer suspected vulnerabilities away from issue forms and into a private security reporting path.

### Console and CLI polish

- Runnable console apps can now opt into a command-first output contract so public CLI help and validation flows stay quiet instead of printing Generic Host lifecycle chatter.
- RazorWire CLI now uses that contract for `--help`, `export --help`, invalid option output, and missing-source validation while still preserving command-owned export progress logs.
- RazorWire CLI now names export seed-route files with `-r|--seeds`, matching the seed terminology used throughout the exporter and docs.
- The shared console startup seam now exposes `ConsoleOptions` and `ConsoleOutputMode`, so future public Runnable CLIs can adopt the same behavior without forking startup logic.
- RazorWire CLI now has a first-class .NET tool package contract with the `razorwire` command, supports exact-version `dnx` execution from published or explicit local package sources, and verifies the installed tool path through help and sample export smoke tests. Public package publishing remains manual until the coordinated release automation tracked in #161 lands.
- Project exports now disable persistent MSBuild build servers during CLI-controlled publish and assembly-name probes so captured tool output cannot hang on reused build nodes.
- RazorWire CLI process cleanup now waits for asynchronous stdout and stderr callbacks to flush before disposing launched target processes, which keeps short-lived command output observable in tests and diagnostics.

### Dependency maintenance

- The centrally managed `YamlDotNet` dependency now targets `17.0.1`, and the affected PackageIndex, RazorDocs, and Aspire lock files have been regenerated.

### Web host development defaults

- Runnable web hosts now choose a deterministic localhost-only development URL when no endpoint is configured, while production, staging, container, and appsettings-based endpoint choices remain untouched.

### RazorWire package guidance

- RazorWire now has a generated UI design contract for package-owned nodes. The contract separates RazorWire UI from app-authored markup and RazorDocs chrome, establishes `data-rw-*` attributes plus `--rw-ui-*` custom properties as the default styling surface, and documents global, form-level, and target-level override expectations for future generated UI.

### RazorDocs product example

- Runnable's own release pages now double as a working RazorDocs example for consumers who want better release notes.
- RazorDocs pages can now expose typed `On this page` outlines, explicit proof-path previous/next links, related-page cards, and sidebar anchor navigation from harvested metadata instead of scraping rendered HTML.
- Public docs navigation now groups pages by intent-first sections, preserves authored editorial breadcrumbs, and keeps Start Here recovery links hidden when that section is unavailable.
- The release contract is designed so future tooling can generate both a changelog entry and a blog-style tagged release note from the same underlying signals.
- RazorDocs now rewrites authored doc links from a harvested target manifest instead of broad suffix heuristics, so normal site links such as `../privacy.html` stay untouched and missing doc targets do not become broken `/docs/...` routes.

## Migration watch

There is no tagged migration guide yet because Runnable has not cut `v0.1.0`. Until then:

- breaking changes should be called out here as soon as they land
- the stable policy lives in [Pre-1.0 upgrade policy](./upgrade-policy.md)
- finalized migration steps move into the tagged release note when the version ships
- custom RazorDocs harvesters that want detail-page outlines and search heading metadata should populate `DocNode.Outline`; pages without outline metadata continue to render without the optional outline section

## Proof artifacts

- [Release hub](./README.md)
- [Changelog](../CHANGELOG.md)
- [Pre-1.0 upgrade policy](./upgrade-policy.md)
- [Release authoring checklist](./release-authoring-checklist.md)

## Before the first tag

The current intent is that everything already in this repository can be part of `v0.1.0` when Runnable is ready to release. This page is where that pile becomes visible and reviewable before the tag exists.
