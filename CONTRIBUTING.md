# Contributing to Runnable

Runnable is putting its release contract in place before the first tagged version. This file explains the contribution rules that feed the public release surface.

## Release contract

- Runnable releases the monorepo in unison. Packages, CLI tooling, examples, and docs-facing behavior all roll into the same next version.
- Pull request titles that land on `main` must follow Conventional Commits. The squash-merge title is the durable signal for future automation and changelog grouping.
- Update [`releases/unreleased.md`](./releases/unreleased.md) whenever a pull request changes behavior, usage guidance, release policy, examples, or docs consumers would care about in release notes.
- Maintainers may apply the `no-unreleased-entry` label only for changes that do not belong in the public release story, such as repo administration or workflow-only cleanup.

## Writing release notes

- Start from the public [release hub](./releases/README.md).
- Keep [`CHANGELOG.md`](./CHANGELOG.md) compact. It is the ledger, not the full story.
- Put detailed adoption notes in the current unreleased page or a tagged release page under [`releases/`](./releases/README.md).
- Record breaking or behavior-changing updates in the unreleased page even before `v0.1.0`. Finalized migration guidance moves into the tagged release page when the version ships.

## Maintainer workflow

- Use the [release authoring checklist](./releases/release-authoring-checklist.md) when preparing a release.
- Use the [tagged release template](./releases/templates/tagged-release-template.md) when cutting the first versioned release note.
- Keep private maintainer-only recovery notes outside harvested public docs. In this repository, `.github/` is the safe home for that material.

## Local verification

Build and test the full solution before pushing substantive changes:

```bash
dotnet build
dotnet test --no-build
./scripts/coverage-solution.sh
```
