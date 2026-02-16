using FakeItEasy;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorWire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class RazorDocsWebModuleTests
{
    private readonly RazorDocsWebModule _module;

    public RazorDocsWebModuleTests()
    {
        _module = new RazorDocsWebModule();
    }

    [Fact]
    public void Properties_ShouldReturnDefaultValues()
    {
        Assert.True(_module.IncludeAsApplicationPart);
    }

    [Fact]
    public void RegisterDependentModules_ShouldAddRazorWireModule()
    {
        // Arrange
        var builder = new ModuleDependencyBuilder();

        // Act
        _module.RegisterDependentModules(builder);

        // Assert
        Assert.Contains(builder.Modules, m => m is RazorWireWebModule);
    }

    [Fact]
    public void RegisterDependentModules_ShouldAddCachingModule()
    {
        var builder = new ModuleDependencyBuilder();

        _module.RegisterDependentModules(builder);

        Assert.Contains(builder.Modules, m => m is RunnableCachingModule);
    }


    [Fact]
    public void ConfigureServices_ShouldRegisterRequiredServices()
    {
        // Arrange
        var rootModuleFake = A.Fake<IRunnableHostModule>();
        var envFake = A.Fake<IEnvironmentProvider>();
        var context = new StartupContext(Array.Empty<string>(), rootModuleFake, "TestApp", envFake);
        var services = new ServiceCollection();

        // Act
        _module.ConfigureServices(context, services);

        // Assert
        Assert.Contains(
            services,
            s => s.ServiceType == typeof(IDocHarvester) && s.ImplementationType == typeof(MarkdownHarvester));
        Assert.Contains(
            services,
            s => s.ServiceType == typeof(IDocHarvester) && s.ImplementationType == typeof(CSharpDocHarvester));
        Assert.Contains(services, s => s.ServiceType == typeof(DocAggregator));
        Assert.Contains(
            services,
            s => s.ServiceType == typeof(Ganss.Xss.IHtmlSanitizer) && s.Lifetime == ServiceLifetime.Singleton);

        using var serviceProvider = services.BuildServiceProvider();
        var sanitizer = Assert.IsType<Ganss.Xss.HtmlSanitizer>(
            serviceProvider.GetRequiredService<Ganss.Xss.IHtmlSanitizer>());
        Assert.Contains("section", sanitizer.AllowedTags);
        Assert.Contains("article", sanitizer.AllowedTags);
        Assert.Contains("header", sanitizer.AllowedTags);
        Assert.Contains("details", sanitizer.AllowedTags);
        Assert.Contains("summary", sanitizer.AllowedTags);
        Assert.Contains("class", sanitizer.AllowedAttributes);
        Assert.Contains("id", sanitizer.AllowedAttributes);
        Assert.Contains("open", sanitizer.AllowedAttributes);
    }

    [Fact]
    public void ConfigureEndpoints_ShouldMapDefaultRoute()
    {
        var context = CreateStartupContext();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(DocsController).Assembly);
        using var app = builder.Build();
        var routeBuilder = (IEndpointRouteBuilder)app;

        _module.ConfigureEndpoints(context, routeBuilder);

        var routePatterns = routeBuilder.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(pattern => !string.IsNullOrEmpty(pattern))
            .ToList();

        Assert.Contains("docs", routePatterns);
        Assert.Contains("docs/search", routePatterns);
        Assert.Contains("docs/search-index.json", routePatterns);
        Assert.Contains("docs/{*path}", routePatterns);
        Assert.Contains("{controller=Docs}/{action=Index}/{path?}", routePatterns);
    }

    [Fact]
    public void ConfigureEndpoints_ShouldRegisterSearchBeforeCatchAll_InSourceOrder()
    {
        var sourcePath = Path.Combine(
            TestPathUtils.FindRepoRoot(AppContext.BaseDirectory),
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs",
            "RazorDocsWebModule.cs");

        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath));
        var root = syntaxTree.GetRoot();
        var configureEndpointsMethod = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(method => method.Identifier.Text == "ConfigureEndpoints");

        Assert.NotNull(configureEndpointsMethod);

        var routePatterns = configureEndpointsMethod!
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(
                invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess
                              && memberAccess.Name.Identifier.Text == "MapControllerRoute")
            .Select(
                invocation => invocation.ArgumentList.Arguments
                    .FirstOrDefault(argument => argument.NameColon?.Name.Identifier.Text == "pattern")
                    ?.Expression as LiteralExpressionSyntax)
            .Where(patternLiteral => patternLiteral is not null)
            .Select(patternLiteral => patternLiteral!.Token.ValueText)
            .ToList();

        var searchIndex = routePatterns.IndexOf("docs/search");
        var catchAllIndex = routePatterns.IndexOf("docs/{*path}");

        Assert.True(searchIndex >= 0, "Expected docs/search route declaration.");
        Assert.True(catchAllIndex >= 0, "Expected docs/{*path} route declaration.");
        Assert.True(searchIndex < catchAllIndex, "docs/search must be registered before docs/{*path}.");
    }

    [Fact]
    public void HostAndAppConfigureMethods_ShouldNotThrow()
    {
        var context = CreateStartupContext();
        var hostBuilder = A.Fake<IHostBuilder>();
        var appBuilder = A.Fake<Microsoft.AspNetCore.Builder.IApplicationBuilder>();

        _module.ConfigureHostBeforeServices(context, hostBuilder);
        _module.ConfigureHostAfterServices(context, hostBuilder);
        _module.ConfigureWebApplication(context, appBuilder);

        Assert.True(true);
    }

    private static StartupContext CreateStartupContext()
    {
        var rootModuleFake = A.Fake<IRunnableHostModule>();
        var envFake = A.Fake<IEnvironmentProvider>();
        return new StartupContext(Array.Empty<string>(), rootModuleFake, "TestApp", envFake);
    }
}
