namespace DependencyInjectionControllers;

public class MyDependencyService : IMyDependencyService
{
    public string GetData() => "Hello from MyDependencyService!";
}
