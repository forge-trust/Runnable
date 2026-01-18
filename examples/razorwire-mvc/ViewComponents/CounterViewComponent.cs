using Microsoft.AspNetCore.Mvc;
using System.Threading;

namespace RazorWireWebExample.ViewComponents;

public class CounterViewComponent : ViewComponent
{
    private static int _count;

    public static int Count => _count;

    public static void Increment() => Interlocked.Increment(ref _count);

    public IViewComponentResult Invoke()
    {
        return View(_count);
    }
}
