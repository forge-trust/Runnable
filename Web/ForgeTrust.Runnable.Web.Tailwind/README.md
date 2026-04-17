# ForgeTrust.Runnable.Web.Tailwind

Tailwind CSS integration for Runnable web applications with zero Node.js dependency.

## Overview

This package wires the Tailwind standalone CLI into the Runnable web build pipeline so your app can compile generated CSS during builds and run Tailwind in watch mode during development.

## Features

- **No Node.js required**: Uses the official standalone Tailwind CLI binaries.
- **RID-aware runtime packages**: Pulls in the platform-specific runtime package automatically when the package is restored.
- **Build integration**: Compiles `wwwroot/css/app.css` to `wwwroot/css/site.gen.css` by default.
- **Development watch mode**: Starts Tailwind in `--watch` during development when you register the service.

## Usage

Install the package and register Tailwind in your web module:

```csharp
services.AddTailwind(options =>
{
    options.InputPath = "wwwroot/css/app.css";
    options.OutputPath = "wwwroot/css/site.gen.css";
});
```

Reference the generated stylesheet from your layout:

```html
<link rel="stylesheet" href="~/css/site.gen.css" asp-append-version="true" />
```

## Notes

- The generated CSS file is intended to be build output and is commonly ignored in source control.
- The platform-specific `ForgeTrust.Runnable.Web.Tailwind.Runtime.*` packages are support packages consumed transitively by this package and are not usually installed directly.
