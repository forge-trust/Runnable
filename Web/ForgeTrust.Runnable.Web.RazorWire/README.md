# RazorWire

**RazorWire** is a library for building modern, reactive web applications in ASP.NET Core using an **HTML-over-the-wire** approach. It integrates seamlessly with the Runnable module system to provide real-time updates, reactive components ("Islands"), and enhanced form handling without the complexity of a full SPA framework.

## Core Concepts

### 🏝️ Islands (Turbo Frames)
Islands are isolated regions of a page that can be loaded, reloaded, or updated independently. This is achieved using Turbo Frames, allowing you to decompose complex pages into smaller, manageable pieces.

### 📡 Real-time Streaming (Turbo Streams & SSE)
RazorWire uses Server-Sent Events (SSE) to push updates from the server to one or more clients. These updates are delivered as Turbo Streams, which can perform actions like `append`, `prepend`, `replace`, or `remove` on specific DOM elements.

### ⚡ Form Enhancement

Standard HTML forms are enhanced to perform partial-page updates. Instead of a full page reload or redirect, forms can return Turbo Stream actions to update only the necessary parts of the UI.

## Getting Started

### 1. Add the Module
To enable RazorWire in your Runnable application, add the `RazorWireWebModule` to your root module or application startup:

```csharp
public class MyRootModule : IRunnableWebModule
{
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.Add<RazorWireWebModule>();
    }
}
```

### 2. Configure Services (Optional)
You can customize RazorWire behavior via `RazorWireOptions`:

```csharp
services.AddRazorWire(options => {
    options.Streams.BasePath = "/custom-stream-path";
});
```

### 3. Use in Controllers
Return reactive responses directly from your MVC controllers:

```csharp
// Return an Island (Frame)
return RazorWireBridge.Frame(this, "my-island-id", "_PartialViewName", model);

// Return a Stream update
return this.RazorWireStream()
    .Append("chat-list", "<li>New Message</li>")
    .BuildResult();
```

## API Reference

### `RazorWireBridge`
- **`Frame(controller, id, viewName, model)`**: Returns a partial view wrapped in a `<turbo-frame>` with the specified ID.

### `IRazorWireStreamHub`
The central hub for publishing real-time updates to connected clients.
- **`PublishAsync(channel, content)`**: Broadcasts a Turbo Stream fragment to all subscribers of a specific channel.

### `this.RazorWireStream()` (Controller Extension)
A fluent API for building Turbo Stream responses:
- **`Append(target, content)`**: Adds content to the end of the target element.
- **`Prepend(target, content)`**: Adds content to the beginning.
- **`Replace(target, content)`**: Replaces the target element entirely.
- **`Update(target, content)`**: Replaces the inner content of the target.
- **`Remove(target)`**: Removes the target element.

## TagHelpers

RazorWire provides several TagHelpers to simplify working with Turbo Frames and Streams in Razor views.

### `rw:island`
Wraps content in a `<turbo-frame>`.
- **`id`**: Unique identifier for the island.
- **`src`**: URL to load content from (optional).
- **`loading`**: Set to `lazy` for deferred loading.
- **`permanent`**: Persists the element across page transitions.

```html
<rw:island id="sidebar" src="/reactivity/sidebar" loading="lazy">
    <p>Loading sidebar...</p>
</rw:island>
```

### `rw:form`
Enhances a standard form to return Turbo Stream updates.
- **`target`**: The frame to target (optional).

```html
<rw:form asp-action="Join" method="post">
    <input type="text" name="Username" />
    <button type="submit">Join</button>
</rw:form>
```

### `rw:scripts`
Injects necessary RazorWire and Turbo client scripts.

```html
<rw:scripts />
```

## Client-Side Interop (Hybrid Components)

RazorWire supports a hybrid approach where server-rendered "Islands" can be augmented with client-side modules (e.g., React, Vue, or Vanilla JS).

- **`client-module`**: The name of the JavaScript module to initialize.
- **`client-strategy`**: Initialization strategy (e.g., `load`, `idle`, `visible`).
- **`client-props`**: JSON properties passed to the client module.

```html
<rw:island id="interactive-chart" 
           client-module="ChartComponent" 
           client-strategy="visible" 
           client-props='{ "data": [1, 2, 3] }'>
</rw:island>
```

## Static Export
RazorWire can be used to generate static or hybrid sites. For more details, see the [RazorWire CLI](../../Web/ForgeTrust.Runnable.Web.RazorWire.Cli/README.md).

## Examples

For a practical demonstration of these features, see the [RazorWire MVC Example](../../examples/razorwire-mvc/README.md).
