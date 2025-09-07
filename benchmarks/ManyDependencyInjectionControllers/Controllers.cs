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

// Group 1: 1-5 (Singleton + Transient)
[Route("api/many/injected/1")]
public sealed class ManyInjected01Controller(IMyDependencyService s, ISingletonGuidService singleton, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, transient: _transient);
}

[Route("api/many/injected/2")]
public sealed class ManyInjected02Controller(IMyDependencyService s, ISingletonGuidService singleton, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, transient: _transient);
}

[Route("api/many/injected/3")]
public sealed class ManyInjected03Controller(IMyDependencyService s, ISingletonGuidService singleton, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, transient: _transient);
}

[Route("api/many/injected/4")]
public sealed class ManyInjected04Controller(IMyDependencyService s, ISingletonGuidService singleton, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, transient: _transient);
}

[Route("api/many/injected/5")]
public sealed class ManyInjected05Controller(IMyDependencyService s, ISingletonGuidService singleton, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, transient: _transient);
}

// Group 2: 6-10 (Scoped + Transient)
[Route("api/many/injected/6")]
public sealed class ManyInjected06Controller(IMyDependencyService s, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(scoped: _scoped, transient: _transient);
}

[Route("api/many/injected/7")]
public sealed class ManyInjected07Controller(IMyDependencyService s, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(scoped: _scoped, transient: _transient);
}

[Route("api/many/injected/8")]
public sealed class ManyInjected08Controller(IMyDependencyService s, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(scoped: _scoped, transient: _transient);
}

[Route("api/many/injected/9")]
public sealed class ManyInjected09Controller(IMyDependencyService s, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(scoped: _scoped, transient: _transient);
}

[Route("api/many/injected/10")]
public sealed class ManyInjected10Controller(IMyDependencyService s, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(scoped: _scoped, transient: _transient);
}

// Group 3: 11-15 (Factory + Generic<TController>)
[Route("api/many/injected/11")]
public sealed class ManyInjected11Controller(IMyDependencyService s, IFactoryCreatedService factory, IGenericService<ManyInjected11Controller> generic) : BaseInjectedController(s)
{
    private readonly IFactoryCreatedService _factory = factory;
    private readonly IGenericService<ManyInjected11Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(factory: _factory, generic: _generic);
}

[Route("api/many/injected/12")]
public sealed class ManyInjected12Controller(IMyDependencyService s, IFactoryCreatedService factory, IGenericService<ManyInjected12Controller> generic) : BaseInjectedController(s)
{
    private readonly IFactoryCreatedService _factory = factory;
    private readonly IGenericService<ManyInjected12Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(factory: _factory, generic: _generic);
}

[Route("api/many/injected/13")]
public sealed class ManyInjected13Controller(IMyDependencyService s, IFactoryCreatedService factory, IGenericService<ManyInjected13Controller> generic) : BaseInjectedController(s)
{
    private readonly IFactoryCreatedService _factory = factory;
    private readonly IGenericService<ManyInjected13Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(factory: _factory, generic: _generic);
}

[Route("api/many/injected/14")]
public sealed class ManyInjected14Controller(IMyDependencyService s, IFactoryCreatedService factory, IGenericService<ManyInjected14Controller> generic) : BaseInjectedController(s)
{
    private readonly IFactoryCreatedService _factory = factory;
    private readonly IGenericService<ManyInjected14Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(factory: _factory, generic: _generic);
}

[Route("api/many/injected/15")]
public sealed class ManyInjected15Controller(IMyDependencyService s, IFactoryCreatedService factory, IGenericService<ManyInjected15Controller> generic) : BaseInjectedController(s)
{
    private readonly IFactoryCreatedService _factory = factory;
    private readonly IGenericService<ManyInjected15Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(factory: _factory, generic: _generic);
}

// Group 4: 16-20 (Singleton + Scoped + Transient)
[Route("api/many/injected/16")]
public sealed class ManyInjected16Controller(IMyDependencyService s, ISingletonGuidService singleton, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, scoped: _scoped, transient: _transient);
}

[Route("api/many/injected/17")]
public sealed class ManyInjected17Controller(IMyDependencyService s, ISingletonGuidService singleton, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, scoped: _scoped, transient: _transient);
}

[Route("api/many/injected/18")]
public sealed class ManyInjected18Controller(IMyDependencyService s, ISingletonGuidService singleton, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, scoped: _scoped, transient: _transient);
}

[Route("api/many/injected/19")]
public sealed class ManyInjected19Controller(IMyDependencyService s, ISingletonGuidService singleton, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, scoped: _scoped, transient: _transient);
}

[Route("api/many/injected/20")]
public sealed class ManyInjected20Controller(IMyDependencyService s, ISingletonGuidService singleton, IScopedGuidService scoped, ITransientGuidService transient) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly IScopedGuidService _scoped = scoped;
    private readonly ITransientGuidService _transient = transient;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, scoped: _scoped, transient: _transient);
}

// Group 5: 21-25 (Options + Generic<TController>, and a couple with factory/singleton mixed)
[Route("api/many/injected/21")]
public sealed class ManyInjected21Controller(IMyDependencyService s, IOptionsProviderService options, IGenericService<ManyInjected21Controller> generic) : BaseInjectedController(s)
{
    private readonly IOptionsProviderService _options = options;
    private readonly IGenericService<ManyInjected21Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(generic: _generic, options: _options);
}

[Route("api/many/injected/22")]
public sealed class ManyInjected22Controller(IMyDependencyService s, IOptionsProviderService options, IGenericService<ManyInjected22Controller> generic) : BaseInjectedController(s)
{
    private readonly IOptionsProviderService _options = options;
    private readonly IGenericService<ManyInjected22Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(generic: _generic, options: _options);
}

[Route("api/many/injected/23")]
public sealed class ManyInjected23Controller(IMyDependencyService s, IOptionsProviderService options, IGenericService<ManyInjected23Controller> generic) : BaseInjectedController(s)
{
    private readonly IOptionsProviderService _options = options;
    private readonly IGenericService<ManyInjected23Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(generic: _generic, options: _options);
}

[Route("api/many/injected/24")]
public sealed class ManyInjected24Controller(IMyDependencyService s, ISingletonGuidService singleton, IOptionsProviderService options, IFactoryCreatedService factory) : BaseInjectedController(s)
{
    private readonly ISingletonGuidService _singleton = singleton;
    private readonly IOptionsProviderService _options = options;
    private readonly IFactoryCreatedService _factory = factory;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(singleton: _singleton, factory: _factory, options: _options);
}

[Route("api/many/injected/25")]
public sealed class ManyInjected25Controller(IMyDependencyService s, IOptionsProviderService options, IFactoryCreatedService factory, IGenericService<ManyInjected25Controller> generic) : BaseInjectedController(s)
{
    private readonly IOptionsProviderService _options = options;
    private readonly IFactoryCreatedService _factory = factory;
    private readonly IGenericService<ManyInjected25Controller> _generic = generic;
    [HttpGet]
    public ActionResult<string> Get() => Service.GetData() + "|" + ManySummary.Compose(factory: _factory, generic: _generic, options: _options);
}
