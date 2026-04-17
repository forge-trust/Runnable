# ForgeTrust.Runnable.Web.RazorDocs

Reusable Razor Class Library package for harvesting and serving repository documentation inside a Runnable web application.

## What it provides

- `RazorDocsWebModule` for wiring the docs UI into a Runnable web host
- `AddRazorDocs()` for typed options binding and core service registration
- `DocAggregator` plus the built-in markdown and C# harvesters
- Search UI assets and the `/docs` MVC surface used by RazorDocs consumers

## Configuration

Slice 1 supports source-backed docs via `RazorDocsOptions`:

```json
{
  "RazorDocs": {
    "Mode": "Source",
    "Source": {
      "RepositoryRoot": "/path/to/repo"
    }
  }
}
```

If `RazorDocs:Source:RepositoryRoot` is omitted, the package falls back to repository discovery from the app content root. Bundle mode is modeled but intentionally rejected until Slice 2 lands.

## Related Projects

- [ForgeTrust.Runnable.Web.RazorDocs.Standalone](../ForgeTrust.Runnable.Web.RazorDocs.Standalone/README.md) for the runnable/exportable host used in docs export and smoke testing
- [Back to Web List](../README.md)
- [Back to Root](../../README.md)
