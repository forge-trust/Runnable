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

When you run this host without an explicit `--port`, `--urls`, or `ASPNETCORE_URLS`, Runnable Web assigns a deterministic development port from the current workspace path. That keeps sibling worktrees from colliding on the same default localhost URL.

- Use the startup log as the source of truth for the selected local URL.
- Pass `--port 5189` or `--urls http://127.0.0.1:5189` when you intentionally want a fixed address.
- The checked-in launch profile no longer pins a single shared localhost port, because that was the source of cross-worktree QA confusion.

## Related Projects

- [ForgeTrust.Runnable.Web.RazorDocs](../ForgeTrust.Runnable.Web.RazorDocs/README.md) for the reusable docs package
- [Back to Web List](../README.md)
- [Back to Root](../../README.md)
