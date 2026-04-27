# RazorWire

RazorWire lets ASP.NET Core MVC apps update UI by returning Razor fragments from the server instead of building a separate JSON endpoint and client-state rendering loop.

## 60-Second Quickstart

This quickstart assumes you are in a clone of this repository with the .NET 10 SDK installed. The public package install matrix is still being finalized for v0.1, so the fastest way to feel RazorWire today is the in-repo sample.

```bash
dotnet run --project examples/razorwire-mvc/RazorWireWebExample.csproj
```

Open the URL printed in the console and navigate to `/Reactivity`, wait for the `Permanent Island` card to load, then click the `+` button. The `Instance Score` and `Session Score` update in place without a full-page reload.

## Hero Proof

`examples/razorwire-mvc/Views/Shared/Components/Counter/Default.cshtml`

```cshtml
<div id="instance-score-value">@Model</div>
<div id="session-score-value">0</div>

<form asp-controller="Reactivity" asp-action="IncrementCounter" method="post" rw-active="true">
    <input type="hidden" name="clientCount" id="client-count-input" value="0" />
    <button type="submit">+</button>
</form>
```

`examples/razorwire-mvc/Controllers/ReactivityController.cs`

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult IncrementCounter([FromForm] int clientCount)
{
    CounterViewComponent.Increment();
    clientCount++;

    if (Request.IsTurboRequest())
    {
        return this.RazorWireStream()
            .Update("instance-score-value", CounterViewComponent.Count.ToString())
            .Update("session-score-value", clientCount.ToString())
            .ReplacePartial("client-count-input", "_CounterInput", clientCount)
            .BuildResult();
    }

    var referer = Request.Headers["Referer"].ToString();
    return Url.IsLocalUrl(referer) ? Redirect(referer) : RedirectToAction(nameof(Index));
}
```

`examples/razorwire-mvc/Views/Reactivity/_CounterInput.cshtml`

```cshtml
<input type="hidden" name="clientCount" id="client-count-input" value="@Model" />
```

Read the [focused proof path](../../examples/razorwire-mvc/README.md#start-here-return-razor-fragments) for the file-by-file walkthrough. If copying this pattern gives you a bare `400 Bad Request`, anti-forgery is the first thing to check. See [Security & Anti-Forgery](Docs/antiforgery.md).

## Add the Module

Once you already reference the RazorWire package in your app, add `RazorWireWebModule` to your root module:

```csharp
public class MyRootModule : IRunnableWebModule
{
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RazorWireWebModule>();
    }
}
```

## Enable TagHelpers and Scripts

RazorWire markup only lights up when your views import the package TagHelpers and your shared layout renders the client scripts once. Without this step, `rw:island`, `rw:stream-source`, and `rw-active` forms fall back to plain HTML behavior.

`Views/_ViewImports.cshtml`

```cshtml
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, ForgeTrust.Runnable.Web.RazorWire
```

`Views/Shared/_Layout.cshtml`

```cshtml
<head>
    ...
    <rw:scripts />
</head>
```

## Configure Services (Optional)

You can customize RazorWire behavior via `RazorWireOptions`:

```csharp
services.AddRazorWire(options =>
{
    options.Streams.BasePath = "/custom-stream-path";
});
```

## Also Possible

- Keep sidebars and other regions independent with `rw:island`, including lazy loading and `permanent="true"` persistence across page transitions.
- Push live updates to connected clients with `IRazorWireStreamHub` and `rw:stream-source`.
- Return form updates from normal MVC controllers with `this.RazorWireStream()`, not a separate JSON API.
- See the broader [RazorWire MVC Example](../../examples/razorwire-mvc/README.md) for registration, message publishing, islands, and SSE.
- See [Security & Anti-Forgery](Docs/antiforgery.md) for the form-update patterns that matter in production.

## Core Concepts

### Islands

Islands are isolated regions of a page that can load, reload, or update independently. RazorWire renders them as Turbo Frames, so you can decompose a page into smaller Razor-backed units without introducing a separate frontend app.

### Streams and SSE

RazorWire can push Turbo Stream updates to one or more clients over Server-Sent Events. That makes it a good fit for counters, feeds, presence lists, and other UI that should update live while staying server-rendered.

### Form Enhancement

Standard HTML forms can return targeted stream updates instead of full reloads or redirect-first flows. The counter example above is the smallest version of that story: submit a normal MVC form, return RazorWire updates, and change only the DOM you care about.

## Security & Anti-Forgery

Handling anti-forgery tokens correctly is critical when updating forms via Turbo Streams. See [Security & Anti-Forgery](Docs/antiforgery.md) for the detailed patterns and recommendations.

## Development Experience

RazorWire is designed for a fast feedback loop during development:

- Razor Runtime Compilation is automatically enabled in `Development`, so you can edit `.cshtml` files and refresh without rebuilding.
- Local scripts and styles automatically receive version hashes for cache busting, even without `asp-append-version="true"`.

## API Reference

### `RazorWireBridge`

- `Frame(controller, id, viewName, model)` returns a partial view wrapped in a `<turbo-frame>` with the specified ID.
- `FrameComponent(controller, id, componentName)` renders a view component inside a `<turbo-frame>`.

### `IRazorWireStreamHub`

- `PublishAsync(channel, content)` broadcasts a Turbo Stream fragment to every subscriber on a channel.

### `this.RazorWireStream()` (controller extension)

- `Append(target, content)` adds content to the end of the target element.
- `Prepend(target, content)` adds content to the beginning.
- `Replace(target, content)` replaces the target element entirely.
- `Update(target, content)` replaces the inner content of the target.
- `Remove(target)` removes the target element.

## TagHelpers

### `rw:island`

Wraps content in a `<turbo-frame>`.

- `id`: unique identifier for the island.
- `src`: URL to load content from.
- `loading`: load strategy such as `lazy`.
- `permanent`: persists the element across Turbo page transitions.
- `swr`: enables stale-while-revalidate behavior.
- `client-module`: client module path or name to mount for hybrid islands.
- `client-strategy`: mount timing such as `load`, `visible`, or `idle`.
- `client-props`: JSON payload passed to the client module's mount function.

```html
<rw:island id="sidebar" src="/Reactivity/Sidebar" loading="lazy" permanent="true">
    <p>Loading sidebar...</p>
</rw:island>
```

### `form[rw-active]`

Enhances a normal form so Turbo handles the submission and optional frame targeting.

- `rw-active="true"` enables RazorWire form handling.
- `rw-target` sets the target frame when you want to constrain the response.

```html
<form asp-controller="Reactivity" asp-action="IncrementCounter" method="post" rw-active="true">
    <input type="hidden" name="clientCount" value="0" />
    <button type="submit">+</button>
</form>
```

### `rw:stream-source`

Subscribes the page to a RazorWire stream channel.

- `channel`: required channel name.
- `permanent`: keeps the stream source alive across Turbo visits.

```html
<rw:stream-source id="rw-stream-reactivity" channel="reactivity" permanent="true"></rw:stream-source>
```

### `requires-stream`

Marks an element as inactive until a named stream is connected.

```html
<button type="submit" requires-stream="reactivity">Send</button>
```

### `<time rw-type="local">`

Localizes UTC timestamps on the client with the browser's `Intl` APIs.

- `rw-display`: `time`, `date`, `datetime`, or `relative`.
- `rw-format`: `short`, `medium`, `long`, or `full`.

```html
<time datetime="@Model.Timestamp" rw-type="local" rw-display="relative"></time>
```

### `rw:scripts`

Injects the client scripts RazorWire needs, including Turbo and the RazorWire assets.

```html
<rw:scripts />
```

## Utilities

### `StringUtils`

- `ToSafeId(input, appendHash)` sanitizes values for DOM IDs or anchors and can append a deterministic hash for uniqueness.

## Client-Side Interop

RazorWire also supports hybrid islands where a server-rendered region mounts a client module:

```html
<rw:island id="interactive-chart"
           client-module="ChartComponent"
           client-strategy="visible"
           client-props='{ "data": [1, 2, 3] }'>
</rw:island>
```

## Static Export

RazorWire can generate static or hybrid sites. For more details, see the [RazorWire CLI](../../Web/ForgeTrust.Runnable.Web.RazorWire.Cli/README.md).

## Examples

- [Focused proof path: return Razor fragments](../../examples/razorwire-mvc/README.md#start-here-return-razor-fragments)
- [Full RazorWire MVC example](../../examples/razorwire-mvc/README.md)
