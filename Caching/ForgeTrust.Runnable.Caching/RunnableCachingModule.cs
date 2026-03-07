using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ForgeTrust.Runnable.Caching;

/// <summary>
/// A Runnable module that registers the <see cref="IMemo"/> caching services.
/// </summary>
public class RunnableCachingModule : IRunnableModule
{
    /// <inheritdoc />
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<IMemo, Memo>();
    }

    /// <inheritdoc />
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
    }
}
