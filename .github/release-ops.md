# Release contract maintainer notes

This file lives under `.github/` on purpose so RazorDocs does not publish it as public documentation.

## `no-unreleased-entry` label

Use `no-unreleased-entry` only when a pull request does not belong in the public release story, such as:

- repository administration
- workflow-only cleanup
- maintainer ergonomics that do not affect adopters

Do not use it for package behavior, CLI behavior, examples, docs-facing behavior, or release policy changes.

## Deferred automation

The current workflow establishes the contracts that future release automation will consume:

- Conventional Commits PR titles
- one public unreleased proof artifact
- tagged release notes plus a compact changelog
- packageable CLI artifacts such as `ForgeTrust.Runnable.Web.RazorWire.Cli`

Until that automation lands, package docs that show `dnx`, `dotnet tool execute`,
or `dotnet tool install` assume either a manually published package source or an
explicit local package source.

Tracked follow-up: #161, "Automate coordinated monorepo releases from the public release contract".
