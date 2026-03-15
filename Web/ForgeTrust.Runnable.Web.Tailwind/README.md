# Runnable Web Tailwind

`ForgeTrust.Runnable.Web.Tailwind` is an optional MVC/Razor integration package for Runnable web apps.

It provides:
- `RunnableWebTailwindModule` to opt a web app into the Tailwind layout helpers.
- `<tw:styles />` to render the compiled stylesheet using the existing local-asset versioning conventions.

## Usage

Add the module dependency:

```csharp
public void RegisterDependentModules(ModuleDependencyBuilder builder)
{
    builder.AddModule<RunnableWebTailwindModule>();
    builder.AddModule<RazorWireWebModule>();
}
```

Import the TagHelpers in `Views/_ViewImports.cshtml`:

```cshtml
@addTagHelper *, ForgeTrust.Runnable.Web.Tailwind
```

Render the stylesheet in your layout:

```cshtml
<tw:styles />
```

If your compiled stylesheet lives somewhere other than `~/css/site.css`, override it via options:

```csharp
services.Configure<TailwindOptions>(options => options.StylesheetPath = "~/docs/site.css");
```

## Building CSS

Use the RazorWire CLI's standalone Tailwind integration to build CSS without npm:

```bash
dotnet run --project Web/ForgeTrust.Runnable.Web.RazorWire.Cli -- \
  tailwind build \
  --project path/to/MyApp.csproj \
  --input tailwind.css \
  --output wwwroot/css/site.css
```
