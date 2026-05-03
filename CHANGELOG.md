# Changelog

This changelog is the compact release ledger for Runnable. The monorepo ships in unison, so each tagged version covers packages, CLI tooling, examples, and docs-facing behavior from this repository together.

## Reading guide

- `Unreleased` tracks the next coordinated version and points to the living release note.
- Future tagged sections will use the shape `## x.y.z - YYYY-MM-DD`.
- Every tagged section will link to a matching narrative release note in [`releases/`](./releases/README.md).
- Breaking or behavior-changing updates must record migration guidance here and in the matching release note.

## Unreleased

- Narrative release note: [Upcoming release note](./releases/unreleased.md)
- Upgrade policy: [Pre-1.0 upgrade policy](./releases/upgrade-policy.md)
- Authoring workflow: [Release authoring checklist](./releases/release-authoring-checklist.md)

### Added

- Runnable now has a repo-level release contract: a public release hub, an unreleased proof artifact, a pre-1.0 upgrade policy, and a tagged-release template for future versioned notes.
- RazorDocs pages can now render a top-of-page trust bar from structured metadata so release notes and upgrade guidance can show status, safety context, and provenance without custom page code.
- RazorDocs now supports metadata-driven page wayfinding: harvested outlines, explicit proof-path previous/next links, related pages, and sidebar anchor navigation.
- The root README now includes a single hello-world web quickstart with an explicit local port and a concrete expected response.
- Runnable now ships GitHub issue templates for bug reports and documentation feedback.

### Changed

- Runnable now treats the whole monorepo as one coordinated release surface. Packages, CLI tools, examples, and docs-facing behavior all roll into the same upcoming version.
- Pull requests are expected to use Conventional Commits titles and to update `releases/unreleased.md` unless maintainers explicitly opt out.
- Markdown-only changes on `main` now trigger the build-and-export workflow so release-note and policy updates publish with the docs surface.
- RazorDocs landing curation now renders reader-intent `featured_page_groups` instead of one flat featured list.
- The PackageIndex generator now has a successful `--help`/`-h` path with command and option guidance instead of a bare usage failure.
- The conventional browser 404 page now favors user recovery, including documentation search for missing docs routes and a home link for other missing pages.

### Migration

- Runnable has not cut `v0.1.0` yet, so there is no tagged migration guide today.
- Before `v0.1.0`, any breaking or behavior-changing update should record provisional guidance in [`releases/unreleased.md`](./releases/unreleased.md) and move finalized steps into the tagged release note once the version ships.
- RazorDocs authors using `featured_pages` should migrate to `featured_page_groups`; the old flat field now logs a warning and no longer renders.

## No tagged releases yet

Runnable is still defining its first release boundary. The first tagged section will be added when the project is ready to cut `v0.1.0`.
