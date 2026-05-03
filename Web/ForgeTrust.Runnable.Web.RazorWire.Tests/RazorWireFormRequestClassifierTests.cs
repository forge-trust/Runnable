using System.Text;
using ForgeTrust.Runnable.Web.RazorWire.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class RazorWireFormRequestClassifierTests
{
    [Fact]
    public async Task IsRazorWireFormRequestAsync_WithHeader_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[RazorWireFormHeaders.FormRequest] = "true";
        var classifier = CreateClassifier();

        var result = await classifier.IsRazorWireFormRequestAsync(context.Request);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRazorWireFormRequestAsync_WithUrlEncodedHiddenMarker_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        var body = "__RazorWireForm=1&name=value";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.ContentLength = Encoding.UTF8.GetByteCount(body);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var classifier = CreateClassifier();

        var result = await classifier.IsRazorWireFormRequestAsync(context.Request);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRazorWireFormRequestAsync_WithUnknownContentLength_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        var body = "__RazorWireForm=1&name=value";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var classifier = CreateClassifier();

        var result = await classifier.IsRazorWireFormRequestAsync(context.Request);

        Assert.False(result);
    }

    [Fact]
    public async Task IsRazorWireFormRequestAsync_WithAlreadyParsedForm_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Features.Set<IFormFeature>(new FormFeature(new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["__RazorWireForm"] = "true"
            })));
        var classifier = CreateClassifier();

        var result = await classifier.IsRazorWireFormRequestAsync(context.Request);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRazorWireFormRequestAsync_WithMultipartWithoutHeader_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data; boundary=test";
        var classifier = CreateClassifier();

        var result = await classifier.IsRazorWireFormRequestAsync(context.Request);

        Assert.False(result);
    }

    [Fact]
    public async Task IsRazorWireFormRequestAsync_WithNonRazorWireTurboForm_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Accept = "text/vnd.turbo-stream.html";
        var classifier = CreateClassifier();

        var result = await classifier.IsRazorWireFormRequestAsync(context.Request);

        Assert.False(result);
    }

    private static RazorWireFormRequestClassifier CreateClassifier()
    {
        return new RazorWireFormRequestClassifier(NullLogger<RazorWireFormRequestClassifier>.Instance);
    }
}
