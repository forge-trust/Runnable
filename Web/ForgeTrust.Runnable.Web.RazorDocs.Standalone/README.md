# ForgeTrust.Runnable.Web.RazorDocs.Standalone

Runnable host for serving or exporting RazorDocs as an application.

## What it is for

This project is the thin executable wrapper around the reusable [ForgeTrust.Runnable.Web.RazorDocs](../ForgeTrust.Runnable.Web.RazorDocs/README.md) package. It exists so the docs surface can run as:

- a local standalone site during development
- the export target in CI
- a smoke-testable host that proves the package seam stays honest

## Entry Point

The app boots through [`Program.cs`](./Program.cs) and delegates the actual docs wiring to `RazorDocsWebModule` from the package project.

## Related Projects

- [ForgeTrust.Runnable.Web.RazorDocs](../ForgeTrust.Runnable.Web.RazorDocs/README.md) for the reusable docs package
- [Back to Web List](../README.md)
- [Back to Root](../../README.md)
