using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.ViewComponents;

public class CounterViewComponent : ViewComponent
{
    private static int _count = 0;

    public static int Count => _count;

    public static void Increment() => _count++;

    public IViewComponentResult Invoke()
    {
        return View(_count);
    }
}
