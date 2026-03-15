using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class TailwindExecutableResolverTests
{
    [Fact]
    public async Task ResolveAsync_Should_Use_Explicit_Executable_Path_When_Provided()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var executablePath = Path.Combine(tempDir, "tailwindcss");
        await File.WriteAllTextAsync(executablePath, "binary");

        try
        {
            var resolver = new TailwindExecutableResolver(
                NullLogger<TailwindExecutableResolver>.Instance,
                new StaticHttpClientFactory(_ => throw new InvalidOperationException("HTTP should not be used.")));

            var resolved = await resolver.ResolveAsync(
                new TailwindExecutableRequest(TailwindDefaults.Version, executablePath, null),
                CancellationToken.None);

            Assert.Equal(executablePath, resolved);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ResolveAsync_Should_Download_Executable_When_Missing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var resolver = new TailwindExecutableResolver(
                NullLogger<TailwindExecutableResolver>.Instance,
                new StaticHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent("binary"u8.ToArray())
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                    }
                }));

            var resolved = await resolver.ResolveAsync(
                new TailwindExecutableRequest(TailwindDefaults.Version, null, tempDir),
                CancellationToken.None);

            Assert.True(File.Exists(resolved));
            Assert.Equal("binary", await File.ReadAllTextAsync(resolved));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private sealed class StaticHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StaticHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StaticHandler(_responseFactory));
        }
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StaticHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
