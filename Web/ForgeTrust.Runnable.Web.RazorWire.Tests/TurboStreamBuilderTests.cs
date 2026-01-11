using ForgeTrust.Runnable.Web.RazorWire.Turbo;
using Xunit;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class TurboStreamBuilderTests
{
    [Fact]
    public void Append_RendersCorrectMarkup()
    {
        // Arrange
        var builder = new TurboStreamBuilder();

        // Act
        var result = builder.Append("target-id", "<div>content</div>").Build();

        // Assert
        Assert.Contains("action=\"append\"", result);
        Assert.Contains("target=\"target-id\"", result);
        Assert.Contains("<template><div>content</div></template>", result);
    }
}
