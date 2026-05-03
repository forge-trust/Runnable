# ForgeTrust.Runnable.Web.RazorDocs.Standalone

Runnable host for serving or exporting RazorDocs as an application.

## What it is for

This project is the thin executable wrapper around the reusable [ForgeTrust.Runnable.Web.RazorDocs](../ForgeTrust.Runnable.Web.RazorDocs/README.md) package. It exists so the docs surface can run as:

- a local standalone site during development
- the export target in CI
- a smoke-testable host that proves the package seam stays honest

## Entry Point

The app boots through [`Program.cs`](./Program.cs) and delegates the actual docs wiring to `RazorDocsWebModule` from the package project.

## Local URL Behavior

When you run this host in `Development` without explicit endpoint configuration, Runnable Web assigns a deterministic localhost-only development URL from the current workspace path. That keeps sibling worktrees from colliding on the same default localhost URL.

- Use the startup log as the source of truth for the selected local URL.
- Pass `--port 5189`, `--urls http://127.0.0.1:5189`, `ASPNETCORE_HTTP_PORTS=5189`, or a `Kestrel:Endpoints` appsettings/environment entry when you intentionally want a fixed address.
- The checked-in launch profile no longer pins a single shared localhost port, because that was the source of cross-worktree QA confusion.

## Contributor Provenance Smoke Testing

The standalone host does not ship a checked-in source or edit target. Hard-coding a public repository or branch in the executable host would make feature-branch and fork smoke tests point readers at the wrong revision.

If you want the live standalone host to exercise the full `Source of truth` strip, provide `RazorDocs:Contributor` explicitly in the environment or app settings that launch the host:

```json
{
  "RazorDocs": {
    "Contributor": {
      "Enabled": true,
      "DefaultBranch": "feature/issue-143",
      "SourceUrlTemplate": "https://github.com/owner/repo/blob/{branch}/{path}",
      "EditUrlTemplate": "https://github.com/owner/repo/edit/{branch}/{path}",
      "LastUpdatedMode": "Git"
    }
  }
}
```

- Set `DefaultBranch` and the repository templates to the exact repo and ref you want readers to reach.
- Slash-separated refs such as `feature/issue-143` are preserved in the generated GitHub-style URLs while still escaping special characters inside each segment.
- For local forks or branch previews, do not reuse upstream `main` unless that is truly the page's source of truth.
- Set `LastUpdatedMode` to `Git` when you want the standalone host to exercise relative freshness too. The package default is `None`, so git-backed timestamps stay opt-in.
- If you cannot provide a trustworthy source or edit destination, leave the templates unset. RazorDocs will still omit unsafe links instead of guessing, and it will omit git-backed `Last updated` unless you explicitly opt into git freshness. Page-level `last_updated_override` metadata can still supply an explicit timestamp.
- The Playwright integration suite launches the standalone host with explicit contributor settings so this runtime configuration seam stays covered.

## Related Projects

- [ForgeTrust.Runnable.Web.RazorDocs](../ForgeTrust.Runnable.Web.RazorDocs/README.md) for the reusable docs package
- [Back to Web List](../README.md)
- [Back to Root](../../README.md)
