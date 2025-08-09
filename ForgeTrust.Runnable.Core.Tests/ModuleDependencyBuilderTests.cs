using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class ModuleDependencyBuilderTests
{
    private class RecordingModule : IRunnableModule
    {
        public static int CallCount;
        public void ConfigureServices(StartupContext context, IServiceCollection services) { }
        public void RegisterDependentModules(ModuleDependencyBuilder builder) => CallCount++;
    }

    [Fact]
    public void AddModule_AddsModuleAndInvokesRegister()
    {
        RecordingModule.CallCount = 0;
        var builder = new ModuleDependencyBuilder();

        builder.AddModule<RecordingModule>();

        Assert.Single(builder.Modules);
        Assert.Equal(1, RecordingModule.CallCount);
    }

    [Fact]
    public void AddModule_DoesNotAddDuplicates()
    {
        RecordingModule.CallCount = 0;
        var builder = new ModuleDependencyBuilder();

        builder.AddModule<RecordingModule>();
        builder.AddModule<RecordingModule>();

        Assert.Single(builder.Modules);
        Assert.Equal(1, RecordingModule.CallCount);
    }

    private class ModuleA : IRunnableModule
    {
        public void ConfigureServices(StartupContext context, IServiceCollection services) { }
        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
            builder.AddModule<ModuleB>();
        }
    }

    private class ModuleB : IRunnableModule
    {
        public void ConfigureServices(StartupContext context, IServiceCollection services) { }
        public void RegisterDependentModules(ModuleDependencyBuilder builder) { }
    }

    [Fact]
    public void AddModule_RegistersDependenciesFromModule()
    {
        var builder = new ModuleDependencyBuilder();

        builder.AddModule<ModuleA>();

        Assert.Equal(2, builder.Modules.Count());
        Assert.Contains(builder.Modules, m => m.GetType() == typeof(ModuleA));
        Assert.Contains(builder.Modules, m => m.GetType() == typeof(ModuleB));
    }
}
