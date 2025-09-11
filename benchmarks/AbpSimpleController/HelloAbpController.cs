using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace AbpSimpleController;

[Route("api/hello")]
public class HelloAbpController: AbpControllerBase
{
    [HttpGet]
    public string Get() => "Hello, World!";
}
