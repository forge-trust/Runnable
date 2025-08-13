using Microsoft.AspNetCore.Mvc;

namespace WebAppExample;

[Route("example")]
[ApiController]
public class ExampleController : ControllerBase
{
    [HttpGet]
    public ActionResult<string> Get()
    {
        return "Hello, World Controller!";
    }
}