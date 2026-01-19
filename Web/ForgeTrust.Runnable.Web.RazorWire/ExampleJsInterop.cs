using Microsoft.JSInterop;

namespace ForgeTrust.Runnable.Web.RazorWire;

// This class provides an example of how JavaScript functionality can be wrapped
// in a .NET class for easy consumption. The associated JavaScript module is
// loaded on demand when first needed.
//
// This class can be registered as scoped DI service and then injected into Blazor
// components for use.

public class ExampleJsInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/ForgeTrust.Runnable.Web.RazorWire/exampleJsInterop.js")
        .AsTask());

    /// <summary>
    /// Shows a JavaScript prompt dialog with the specified message and returns the user's input.
    /// </summary>
    /// <param name="message">The message to display in the prompt dialog.</param>
    /// <returns>The user's input as a string, or null if the dialog was dismissed.</returns>
    public async ValueTask<string?> Prompt(string message)
    {
        var module = await moduleTask.Value;

        return await module.InvokeAsync<string?>("showPrompt", message);
    }

    /// <summary>
    /// Disposes the imported JavaScript module if it has been loaded.
    /// </summary>
    /// <remarks>
    /// If the module was created, awaits the module reference and disposes it to release underlying JavaScript resources.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}