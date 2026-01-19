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
        Assert.Single(results);
        Assert.Contains(results, n => n.Title == "Included");
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
        Assert.Contains(results, n => n.Title == "MyClass" && n.Content.Contains("Class Summary"));
        Assert.Contains(results, n => n.Title == "MyRecord" && n.Content.Contains("Record Summary"));
        Assert.Contains(results, n => n.Title == "MyStruct" && n.Content.Contains("Struct Summary"));
        Assert.Contains(results, n => n.Title == "IMyInterface" && n.Content.Contains("Interface Summary"));
        Assert.Contains(results, n => n.Title == "MyEnum" && n.Content.Contains("Enum Summary"));
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
        var methodNode = results.FirstOrDefault(n => n.Title.StartsWith("SignatureTest.MyMethod"));
        Assert.NotNull(methodNode);

        // Check readable Title: "SignatureTest.MyMethod(int, string, ref bool)"
        Assert.Equal("SignatureTest.MyMethod(int, string, ref bool)", methodNode.Title);

        // Check sanitized Anchor: "SignatureTest-MyMethod-int-string-ref-bool-"
        // Note: The sanitizer generally replaces non-alphanumeric with hyphens and trims.
        // IdentifierRegex is [^a-zA-Z0-9_-]
        // "SignatureTest.MyMethod(int, string, ref bool)" -> "SignatureTest-MyMethod-int--string--ref-bool-"
        // Wait, sanitizer implementation: IdentifierRegex().Replace(input, "-").Trim('-')
        // Let's verify the exact expected string based on logic:
        // Input: "SignatureTest.MyMethod(int, string, ref bool)"
        // . -> -
        // ( -> -
        // , -> -
        // space -> -
        // ) -> -
        // So: "SignatureTest-MyMethod-int--string--ref-bool-"
        // Then Trim('-') -> "SignatureTest-MyMethod-int--string--ref-bool"
        // Let's assert Contains to be safe on exact hyphen count if multiple replacements merge or not (Regex replace does all)
        // Actually Regex Replace replaces EACH char. So multiple spaces = multiple hyphens unless regex handles ranges.
        // The Regex is [^a-zA-Z0-9_-]. It matches one char at a time.
        // So ", " becomes "--". 

        Assert.EndsWith("#SignatureTest-MyMethod-int--string--ref-bool", methodNode.Path);
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
