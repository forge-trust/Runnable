using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class CSharpDocHarvesterTests : IDisposable
{
    private readonly ILogger<CSharpDocHarvester> _loggerFake;
    private readonly CSharpDocHarvester _harvester;
    private readonly string _testRoot;

    public CSharpDocHarvesterTests()
    {
        _loggerFake = A.Fake<ILogger<CSharpDocHarvester>>();
        _harvester = new CSharpDocHarvester(_loggerFake);
        _testRoot = Path.Combine(Path.GetTempPath(), "RazorDocsTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public async Task HarvestAsync_ShouldIgnoreExcludedDirectories()
    {
        // Arrange
        var binDir = Path.Combine(_testRoot, "bin");
        Directory.CreateDirectory(binDir);
        await File.WriteAllTextAsync(Path.Combine(binDir, "Ignored.cs"), "public class Ignored {}");

        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(
            Path.Combine(srcDir, "Included.cs"),
            "/// <summary>Docs</summary>\npublic class Included {}");

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        // Now returns: 1 File Node. The Type Node "Included" is suppressed because it matches the filename "Included".
        Assert.Single(results);
        Assert.Contains(results, n => n.Title == "Included" && string.IsNullOrEmpty(n.ParentPath));
        Assert.DoesNotContain(results, n => n.Title == "Ignored");
    }

    [Fact]
    public async Task HarvestAsync_ShouldExtractDocumentationFromDifferentTypes()
    {
        // Arrange
        var code = @"
            namespace Test;
            
            /// <summary>Class Summary</summary>
            public class MyClass {}

            /// <summary>Record Summary</summary>
            public record MyRecord(int Id);

            /// <summary>Struct Summary</summary>
            public struct MyStruct {}

            /// <summary>Interface Summary</summary>
            public interface IMyInterface {}

            /// <summary>Enum Summary</summary>
            public enum MyEnum { A, B }
        ";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Types.cs"), code);

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        // 1 File Node + 5 Type Nodes = 6 nodes
        Assert.Equal(6, results.Count);

        var fileNode = results.First(n => n.Title == "Types");
        Assert.Contains("Class Summary", fileNode.Content);
        Assert.Contains("Record Summary", fileNode.Content);
        Assert.Contains("Struct Summary", fileNode.Content);
        Assert.Contains("Interface Summary", fileNode.Content);
        Assert.Contains("Enum Summary", fileNode.Content);

        // Sub-nodes should exist but have empty content (navigation stubs)
        Assert.Contains(
            results,
            n => n.Title == "MyClass" && string.IsNullOrEmpty(n.Content) && n.ParentPath == "Types.cs");
    }

    [Fact]
    public async Task HarvestAsync_ShouldHandleMalformedXmlGracefully()
    {
        // Arrange
        var code = @"
            /// <summary>Broken XML
            public class Broken {}
        ";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Broken.cs"), code);

        // Act
        var results = await _harvester.HarvestAsync(_testRoot);

        // Assert
        // The harvester catches the exception and returns null from ExtractDoc,
        // so the node should just be skipped and not added to results.
        Assert.Empty(results);
    }

    [Fact]
    public async Task HarvestAsync_ShouldGenerateReadableSignaturesAndSafeAnchors()
    {
        // Arrange
        var code = @"
            namespace Test;
            public class SignatureTest {
                /// <summary>Method Docs</summary>
                public void MyMethod(int id, string name, ref bool flag) {}
            }
        ";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Signatures.cs"), code);

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        // Sub-node title for method is just the signature
        var methodNode = results.FirstOrDefault(n => n.Title == "MyMethod(int, string, ref bool)");
        Assert.NotNull(methodNode);
        Assert.Equal("MyMethod(int, string, ref bool)", methodNode.Title);
        Assert.EndsWith("#SignatureTest-MyMethod-int-string-ref-bool", methodNode.Path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            try
            {
                Directory.Delete(_testRoot, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
