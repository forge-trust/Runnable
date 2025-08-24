using Microsoft.AspNetCore.Mvc;

namespace SimpleApiController;

[Route("api/hello")]
public class HelloController : ControllerBase
{
    [HttpGet]
    public string Get() => "Hello, World!";
}
