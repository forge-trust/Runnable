using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ForgeTrust.Runnable.Web.Tailwind;

namespace ForgeTrust.Runnable.Web.Tailwind.Tests;

public class TailwindExtensionsTests
{
    [Fact]
    public void AddTailwind_RegistersRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(FakeItEasy.A.Fake<IHostEnvironment>());

        // Act
        services.AddTailwind(opt => 
        {
            opt.InputPath = "test-input.css";
            opt.OutputPath = "test-output.css";
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<TailwindOptions>>();
        Assert.NotNull(options);
        Assert.Equal("test-input.css", options.Value.InputPath);
        Assert.Equal("test-output.css", options.Value.OutputPath);

        Assert.NotNull(serviceProvider.GetService<TailwindCliManager>());
        
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, s => s is TailwindWatchService);
    }
}
