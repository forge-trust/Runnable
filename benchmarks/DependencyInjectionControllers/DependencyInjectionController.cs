using Microsoft.AspNetCore.Mvc;

namespace DependencyInjectionControllers;

[Route("api/injected")]
public class DependencyInjectionController : ControllerBase
{
    private readonly IMyDependencyService _service;

    public DependencyInjectionController(IMyDependencyService service)
    {
        _service = service;
    }

    [HttpGet]
    public ActionResult<string> Get() => _service.GetData();
}
