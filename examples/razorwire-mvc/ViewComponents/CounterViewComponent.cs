using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.ViewComponents;

public class CounterViewComponent : ViewComponent
{
    private static int _count;

    public static int Count => Volatile.Read(ref _count);

    public static void Increment() => Interlocked.Increment(ref _count);

    public IViewComponentResult Invoke()
    {
        return View(Count);
    }
}
