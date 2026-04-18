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

## CI

`ForgeTrust.Runnable.Web.Tailwind` hooks into the normal `dotnet build` and `dotnet publish` pipeline through MSBuild targets, so the default integration does not require a separate `npm install` or `npm run build` step in CI.

If you need to suppress the package-driven build temporarily, set `TailwindEnabled=false` in MSBuild, for example with `dotnet build -p:TailwindEnabled=false` or a project-level `<TailwindEnabled>false</TailwindEnabled>` property.

If you want to keep the package-driven build but point it at a different standalone Tailwind executable, set `TailwindCliPath` to an absolute path or a project-relative file path:

```xml
<PropertyGroup>
  <TailwindCliPath>tools/tailwindcss/tailwindcss</TailwindCliPath>
</PropertyGroup>
```

## Escape hatch (plugin-heavy Tailwind setups)

If your Tailwind configuration depends on npm-only plugins or custom JavaScript tooling, keep your existing Node-based asset pipeline instead of forcing the standalone CLI path.

Disable the package-driven MSBuild integration with `TailwindEnabled=false`, and either omit `services.AddTailwind()` or set `options.Enabled = false` so the development watch service does not start. After that, run your existing npm, pnpm, or yarn Tailwind command as part of your normal frontend build.

If your custom setup still uses the standalone CLI but stores it outside the package runtime layout, prefer `TailwindCliPath` over editing the imported `.targets` file directly.

## Notes

- The generated CSS file is intended to be build output and is commonly ignored in source control.
- The platform-specific `ForgeTrust.Runnable.Web.Tailwind.Runtime.*` packages are support packages consumed transitively by this package and are not usually installed directly.
- Tailwind CLI selection follows the current build host, not `RuntimeIdentifier`, because the standalone CLI runs during the build. Cross-targeted builds still execute the host-compatible binary.
- Windows Arm64 hosts intentionally use the `win-x64` runtime under emulation. There is no `ForgeTrust.Runnable.Web.Tailwind.Runtime.win-arm64` package because Tailwind `v4.1.18` does not ship a native Windows Arm64 standalone CLI.
