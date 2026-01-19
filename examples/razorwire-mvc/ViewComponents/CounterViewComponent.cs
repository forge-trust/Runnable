using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.ViewComponents;

public class CounterViewComponent : ViewComponent
{
    private static int _count;

    public static int Count => Volatile.Read(ref _count);

    /// <summary>
    /// Atomically increments the component's shared counter by one in a thread-safe manner.
    /// </summary>
    public static void Increment() => Interlocked.Increment(ref _count);

    /// <summary>
    /// Renders the view for this component using the current counter value as the model.
    /// </summary>
    /// <returns>A view component result whose model is the current counter value.</returns>
    public IViewComponentResult Invoke()
    {
        return View(Count);
    }
}