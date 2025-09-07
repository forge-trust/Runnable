using DependencyInjectionControllers;
using Microsoft.AspNetCore.Mvc;

namespace ManyDependencyInjectionControllers;

// 25 controllers that resolve IMyDependencyService and expose unique routes
// Routes: /api/many/injected/{n}

public abstract class BaseInjectedController : ControllerBase
{
    protected readonly IMyDependencyService Service;
    protected BaseInjectedController(IMyDependencyService service) => Service = service;
}

[Route("api/many/injected/1")]
public sealed class ManyInjected01Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/2")]
public sealed class ManyInjected02Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/3")]
public sealed class ManyInjected03Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/4")]
public sealed class ManyInjected04Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/5")]
public sealed class ManyInjected05Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/6")]
public sealed class ManyInjected06Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/7")]
public sealed class ManyInjected07Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/8")]
public sealed class ManyInjected08Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/9")]
public sealed class ManyInjected09Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/10")]
public sealed class ManyInjected10Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/11")]
public sealed class ManyInjected11Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/12")]
public sealed class ManyInjected12Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/13")]
public sealed class ManyInjected13Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/14")]
public sealed class ManyInjected14Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/15")]
public sealed class ManyInjected15Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/16")]
public sealed class ManyInjected16Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/17")]
public sealed class ManyInjected17Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/18")]
public sealed class ManyInjected18Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/19")]
public sealed class ManyInjected19Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/20")]
public sealed class ManyInjected20Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/21")]
public sealed class ManyInjected21Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/22")]
public sealed class ManyInjected22Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/23")]
public sealed class ManyInjected23Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/24")]
public sealed class ManyInjected24Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

[Route("api/many/injected/25")]
public sealed class ManyInjected25Controller(IMyDependencyService s) : BaseInjectedController(s)
{
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData();
}

