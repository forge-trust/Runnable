using Autofac;
using Autofac.Builder;
using Autofac.Features.Scanning;

namespace ForgeTrust.Runnable.Dependency.Autofac;

public static class RunnableAutofacExtensions
{
    public static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle>
        RegisterImplementations<TInterface>(this ContainerBuilder builder)
    {
        var assembly = typeof(TInterface).Assembly;
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(TInterface).IsAssignableFrom(t));

        return builder.RegisterTypes(types.ToArray());
    }
}
