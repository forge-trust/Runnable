using ForgeTrust.Runnable.Config;

namespace ForgeTrust.Runnable.config.Tests;

public class DefaultConfigFileLocationProviderTests
{
    [Fact]
    public void Directory_ReturnsCurrentDirectory()
    {
        var provider = new DefaultConfigFileLocationProvider();

        Assert.Equal(Environment.CurrentDirectory, provider.Directory);
    }
}
