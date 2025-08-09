namespace ForgeTrust.Runnable.Core;

public sealed class ModuleDependencyBuilder
{
    private Dictionary<Type, IRunnableModule> _modules = new();
    
    public IEnumerable<IRunnableModule> Modules => _modules.Values;

    public ModuleDependencyBuilder AddModule<T>()
        where T : IRunnableModule, new()
    {
        var type = typeof(T);
        if (!_modules.ContainsKey(type))
        {
            var newModule = new T();
            newModule.RegisterDependentModules(this);
            _modules.Add(type, newModule);
        }

        return this;
    }
}