using Xunit;

namespace ForgeTrust.Runnable.Config.Tests;

public class DefaultConfigFileLocationProviderTests
{
    [Fact]
    public void Directory_ReturnsCurrentDirectory()
    {
        var provider = new DefaultConfigFileLocationProvider();

        Assert.Equal(AppContext.BaseDirectory, provider.Directory);
    }
}
