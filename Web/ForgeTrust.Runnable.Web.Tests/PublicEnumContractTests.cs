namespace ForgeTrust.Runnable.Web.Tests;

public class PublicEnumContractTests
{
    [Theory]
    [InlineData(ConventionalNotFoundPageMode.Auto, 0)]
    [InlineData(ConventionalNotFoundPageMode.Enabled, 1)]
    [InlineData(ConventionalNotFoundPageMode.Disabled, 2)]
    public void ConventionalNotFoundPageMode_NumericValues_AreStable(
        ConventionalNotFoundPageMode value,
        int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    [Theory]
    [InlineData(MvcSupport.None, 0)]
    [InlineData(MvcSupport.Controllers, 1)]
    [InlineData(MvcSupport.ControllersWithViews, 2)]
    [InlineData(MvcSupport.Full, 3)]
    public void MvcSupport_NumericValues_AreStable(MvcSupport value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }
}
