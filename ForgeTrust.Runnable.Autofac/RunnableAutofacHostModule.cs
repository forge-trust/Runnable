﻿using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Autofac;

public abstract class RunnableAutofacHostModule : RunnableAutofacModule, IRunnableHostModule
{
    public virtual void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
        builder.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    }

    public virtual void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
        builder.ConfigureContainer<ContainerBuilder>(b =>
        {
            // Register all dependant modules
            foreach (var m in context.GetDependencies())
            {
                if (m is IModule autofacModule)
                {
                    b.RegisterModule(autofacModule);
                }
            }
            
            b.RegisterModule(this);
        });
    }
    
}
