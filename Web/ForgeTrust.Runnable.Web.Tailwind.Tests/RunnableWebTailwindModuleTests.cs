using FakeItEasy;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ForgeTrust.Runnable.Web.Tailwind.Tests;

public class RunnableWebTailwindModuleTests
{
    [Fact]
    public void IncludeAsApplicationPart_Should_Be_True()
    {
        var module = new RunnableWebTailwindModule();

        Assert.True(module.IncludeAsApplicationPart);
    }

    [Fact]
    public void ConfigureWebOptions_Should_Upgrade_Mvc_To_Views()
    {
        var module = new RunnableWebTailwindModule();
        var context = new StartupContext([], module);
        var options = new WebOptions
        {
            Mvc = new MvcOptions { MvcSupportLevel = MvcSupport.Controllers }
        };

        module.ConfigureWebOptions(context, options);

        Assert.Equal(MvcSupport.ControllersWithViews, options.Mvc.MvcSupportLevel);
    }

    [Fact]
    public void ConfigureServices_Should_Register_Default_Options()
    {
        var module = new RunnableWebTailwindModule();
        var services = new ServiceCollection();

        module.ConfigureServices(new StartupContext([], module), services);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TailwindOptions>>();

        Assert.Equal("~/css/site.css", options.Value.StylesheetPath);
    }
}
