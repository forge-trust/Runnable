using ForgeTrust.Runnable.Web.RazorWire.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class LocalTimeTagHelperTests
{
    [Fact]
    public void Process_RendersTimeElementWithDatetimeAttribute()
    {
        var timestamp = new DateTimeOffset(2026, 1, 24, 12, 30, 0, TimeSpan.Zero);
        var helper = new LocalTimeTagHelper { Value = timestamp };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        Assert.Equal("time", output.TagName);
        Assert.Equal("2026-01-24T12:30:00.0000000+00:00", output.Attributes["datetime"].Value);
    }

    [Fact]
    public void Process_IncludesDataRwLocalTimeAttribute()
    {
        var helper = new LocalTimeTagHelper { Value = DateTimeOffset.UtcNow };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        Assert.True(output.Attributes.ContainsName("data-rw-local-time"));
    }

    [Fact]
    public void Process_DefaultDisplay_DoesNotRenderDisplayAttribute()
    {
        var helper = new LocalTimeTagHelper { Value = DateTimeOffset.UtcNow };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        Assert.False(output.Attributes.ContainsName("data-rw-local-time-display"));
    }

    [Fact]
    public void Process_WithDisplayRelative_RendersDisplayAttribute()
    {
        var helper = new LocalTimeTagHelper
        {
            Value = DateTimeOffset.UtcNow,
            Display = "relative"
        };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        Assert.Equal("relative", output.Attributes["data-rw-local-time-display"].Value);
    }

    [Fact]
    public void Process_WithDisplayDatetime_RendersDisplayAttribute()
    {
        var helper = new LocalTimeTagHelper
        {
            Value = DateTimeOffset.UtcNow,
            Display = "datetime"
        };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        Assert.Equal("datetime", output.Attributes["data-rw-local-time-display"].Value);
    }

    [Fact]
    public void Process_WithFormat_RendersFormatAttribute()
    {
        var helper = new LocalTimeTagHelper
        {
            Value = DateTimeOffset.UtcNow,
            Format = "short"
        };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        Assert.Equal("short", output.Attributes["data-rw-local-time-format"].Value);
    }

    [Fact]
    public void Process_WithNullFormat_DoesNotRenderFormatAttribute()
    {
        var helper = new LocalTimeTagHelper { Value = DateTimeOffset.UtcNow };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        Assert.False(output.Attributes.ContainsName("data-rw-local-time-format"));
    }

    [Fact]
    public void Process_WithDefaultValue_ThrowsArgumentException()
    {
        var helper = new LocalTimeTagHelper();
        var output = CreateOutput();

        Assert.Throws<ArgumentException>(() => helper.Process(CreateContext(), output));
    }

    [Fact]
    public void Process_ConvertsLocalTimeToUtc()
    {
        // Create a timestamp with a non-UTC offset
        var localTimestamp = new DateTimeOffset(2026, 1, 24, 12, 30, 0, TimeSpan.FromHours(-5));
        var helper = new LocalTimeTagHelper { Value = localTimestamp };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        // Should be converted to UTC (17:30:00)
        Assert.Equal("2026-01-24T17:30:00.0000000+00:00", output.Attributes["datetime"].Value);
    }

    [Fact]
    public void Process_DisplayIsCaseInsensitive()
    {
        var helper = new LocalTimeTagHelper
        {
            Value = DateTimeOffset.UtcNow,
            Display = "RELATIVE"
        };
        var output = CreateOutput();

        helper.Process(CreateContext(), output);

        Assert.Equal("relative", output.Attributes["data-rw-local-time-display"].Value);
    }

    [Fact]
    public void Process_RemovesRwLocalAttribute()
    {
        var helper = new LocalTimeTagHelper { Value = DateTimeOffset.UtcNow };
        var output = CreateOutput();
        output.Attributes.Add("rw-local", null);

        helper.Process(CreateContext(), output);

        Assert.False(output.Attributes.ContainsName("rw-local"));
    }

    private static TagHelperContext CreateContext() =>
        new(new TagHelperAttributeList(), new Dictionary<object, object>(), Guid.NewGuid().ToString());

    private static TagHelperOutput CreateOutput() =>
        new(
            "time",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
}
