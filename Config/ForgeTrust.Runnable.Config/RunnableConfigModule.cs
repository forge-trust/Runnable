using System.Reflection;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Config;

public class RunnableConfigModule : IRunnableModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddSingleton<IConfigProvider, DefaultConfigProvider>();

        // Execute the config registration log from the CustomRegistrations
        // because it needs to be done after all modules have been registered
        context.CustomRegistrations.Add(sp =>
        {
            var distinctAssemblies = context.GetDependencies()
                .Select(x => x.GetType().Assembly)
                .Append(context.EntryPointAssembly)
                .Append(context.RootModuleAssembly)
                .Distinct();

            foreach (var assembly in distinctAssemblies)
            {
                RegisterConfigFromAssembly(assembly, services);
            }
        });

    }

    private void RegisterConfigFromAssembly(Assembly assembly, IServiceCollection services)
    {
        var configTypes = assembly.DefinedTypes
            .Where(t => !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters)
            .Where(t => typeof(IConfig).IsAssignableFrom(t.AsType()))
            .Select(t => t.AsType())
            .Distinct()
            .ToList();

        foreach (var type in configTypes)
        {
            services.AddSingleton(type, sp =>
            {
                // Determine the key path (your existing attribute helper)
                var key = ConfigKeyAttribute.GetKeyPath(type);

                // Create instance (supports ctor DI if needed)
                var instance = (IConfig)ActivatorUtilities.CreateInstance(sp, type);

                // Call your init with required services
                instance.Init(
                    sp.GetRequiredService<IConfigProvider>(),
                    sp.GetRequiredService<IEnvironmentProvider>(),
                    key);

                return instance;
            });
        }
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {

    }
}

public class ConfigKeyAttribute : Attribute
{
    public string Key { get; }

    public bool Root { get; }

    public ConfigKeyAttribute(string key, bool root = false)
    {
        Key = key;
        Root = root;
    }

    public ConfigKeyAttribute(Type t)
    {
        Key = GetKeyPath(t);
    }

    public static string? ExtractKey(object obj)
    {
        return ExtractKey(obj.GetType());
    }

    public static string? ExtractKey(Type type)
    {
        var attribute = GetAttribute(type);
        return attribute?.Key;
    }

    private static ConfigKeyAttribute? GetAttribute(
        Type type) => type.GetCustomAttributes(typeof(ConfigKeyAttribute), false).Cast<ConfigKeyAttribute>()
        .FirstOrDefault();

    public static string GetKeyPath(
        Type type)
    {
        var attribute = GetAttribute(type);
        var isRoot = attribute?.Root ?? false;
        var thisMember = attribute?.Key ?? type.Name;
        if (isRoot || type.DeclaringType == null)
        {
            return thisMember;
        }

        var parentPath = GetKeyPath(type.DeclaringType);
        return $"{parentPath}.{thisMember}";
    }
}
