using DependencyInjectionControllers;
using Microsoft.Extensions.DependencyInjection;

namespace ManyDependencyInjectionControllers;

public interface ISingletonGuidService { Guid Id { get; } }
public interface IScopedGuidService { Guid Id { get; } }
public interface ITransientGuidService { Guid Id { get; } }
public interface IFactoryCreatedService { Guid Id { get; } DateTimeOffset CreatedAt { get; } }
public interface IGenericService<T> { string Name { get; } Guid Id { get; } }
public interface IOptionsProviderService { string Value { get; } }

internal sealed class SingletonGuidService : ISingletonGuidService { public Guid Id { get; } = Guid.NewGuid(); }
internal sealed class ScopedGuidService : IScopedGuidService { public Guid Id { get; } = Guid.NewGuid(); }
internal sealed class TransientGuidService : ITransientGuidService { public Guid Id { get; } = Guid.NewGuid(); }
internal sealed class FactoryCreatedService(Guid id, DateTimeOffset createdAt) : IFactoryCreatedService { public Guid Id { get; } = id; public DateTimeOffset CreatedAt { get; } = createdAt; }
internal sealed class GenericService<T> : IGenericService<T> { public string Name { get; } = typeof(T).FullName ?? typeof(T).Name; public Guid Id { get; } = Guid.NewGuid(); }
internal sealed class OptionsProviderService(string value) : IOptionsProviderService { public string Value { get; } = value; }

public static class ManyDiRegistration
{
    public static IServiceCollection AddManyDiServices(this IServiceCollection services)
    {
        services.AddSingleton<ISingletonGuidService, SingletonGuidService>();
        services.AddScoped<IScopedGuidService, ScopedGuidService>();
        services.AddTransient<ITransientGuidService, TransientGuidService>();
        services.AddSingleton<IOptionsProviderService>(_ => new OptionsProviderService("cfg-value"));
        services.AddTransient<IFactoryCreatedService>(_ => new FactoryCreatedService(Guid.NewGuid(), DateTimeOffset.UtcNow));
        services.AddScoped(typeof(IGenericService<>), typeof(GenericService<>));

        // Keep original DI service used by base controllers and benchmarks
        services.AddSingleton<IMyDependencyService, MyDependencyService>();
        return services;
    }
}

internal static class ManySummary
{
    public static string Compose(
        ISingletonGuidService? singleton = null,
        IScopedGuidService? scoped = null,
        ITransientGuidService? transient = null,
        IFactoryCreatedService? factory = null,
        object? generic = null,
        IOptionsProviderService? options = null)
    {
        var parts = new List<string>();
        if (singleton is not null) parts.Add($"singleton:{singleton.Id}");
        if (scoped is not null) parts.Add($"scoped:{scoped.Id}");
        if (transient is not null) parts.Add($"transient:{transient.Id}");
        if (factory is not null) parts.Add($"factory:{factory.Id}@{factory.CreatedAt:O}");
        if (generic is not null)
        {
            var t = generic.GetType();
            var nameProp = t.GetProperty("Name");
            var idProp = t.GetProperty("Id");
            parts.Add($"generic:{nameProp?.GetValue(generic)}:{idProp?.GetValue(generic)}");
        }
        if (options is not null) parts.Add($"options:{options.Value}");
        return string.Join(";", parts);
    }
}

