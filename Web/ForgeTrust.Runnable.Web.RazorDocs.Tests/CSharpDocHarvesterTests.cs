using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class CSharpDocHarvesterTests : IDisposable
{
    private readonly CSharpDocHarvester _harvester;
    private readonly string _testRoot;

    public CSharpDocHarvesterTests()
    {
        var loggerFake = A.Fake<ILogger<CSharpDocHarvester>>();
        _harvester = new CSharpDocHarvester(loggerFake);
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
        var methodNode = results.FirstOrDefault(n => n.Title.StartsWith("Test.SignatureTest.MyMethod"));
        Assert.NotNull(methodNode);

        // Check readable Title: "Test.SignatureTest.MyMethod(int, string, ref bool)"
        Assert.Equal("Test.SignatureTest.MyMethod(int, string, ref bool)", methodNode.Title);

        // Expect sanitized ID from ToSafeId
        Assert.EndsWith("#Test-SignatureTest-MyMethod-int-string-ref-bool", methodNode.Path);
    }

    [Fact]
    public async Task HarvestAsync_ShouldDifferentiateMethodOverloads()
    {
        // Arrange: Create a file with overloaded methods
        var testFile = Path.Combine(_testRoot, "Calculator.cs");
        await File.WriteAllTextAsync(
            testFile,
            @"
namespace TestNamespace;

public class Calculator
{
    /// <summary>Process an integer value.</summary>
    public void Process(int value) { }

    /// <summary>Process a reference to an integer.</summary>
    public void Process(ref int value) { }
}
");

        // Act
        var results = await _harvester.HarvestAsync(_testRoot);
        var processNodes = results.Where(n => n.Title.Contains("Process")).ToList();

        // Assert: Should have two distinct Process methods
        Assert.Equal(2, processNodes.Count);
        Assert.NotEqual(processNodes[0].Path, processNodes[1].Path);

        // Verify both have distinct anchors
        Assert.Contains("Process", processNodes[0].Path);
        Assert.Contains("Process", processNodes[1].Path);
    }

    [Fact]
    public async Task HarvestAsync_ShouldGenerateDistinctQualifiedAnchors_ForTypesWithSameName()
    {
        // Arrange
        var testFile = Path.Combine(_testRoot, "Collision.cs");
        await File.WriteAllTextAsync(
            testFile,
            @"
namespace NamespaceA
{
    /// <summary>Summary A</summary>
    public class SharedName {}
}

namespace NamespaceB
{
    /// <summary>Summary B</summary>
    public class SharedName {}
}
");

        // Act
        var results = await _harvester.HarvestAsync(_testRoot);
        var types = results.Where(n => n.Title == "SharedName").ToList();

        // Assert
        Assert.Equal(2, types.Count);
        Assert.NotEqual(types[0].Path, types[1].Path);

        // Verify qualified anchors
        Assert.Contains("#NamespaceA-SharedName", types.Single(t => t.Content.Contains("Summary A")).Path);
        Assert.Contains("#NamespaceB-SharedName", types.Single(t => t.Content.Contains("Summary B")).Path);
    }

    [Fact]
    public async Task HarvestAsync_ShouldExtractRemarks_WhenPresent()
    {
        // Arrange
        var code = @"
            namespace Test;
            /// <summary>Summary</summary>
            /// <remarks>Remarks here</remarks>
            public class RemarksTest {}
        ";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Remarks.cs"), code);

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var node = results.Single(n => n.Title == "RemarksTest");

        // Assert
        Assert.Contains("<div class='doc-summary", node.Content);
        Assert.Contains("Summary", node.Content);
        Assert.Contains("<div class='doc-remarks", node.Content);
        Assert.Contains("Remarks here", node.Content);
    }

    [Fact]
    public async Task HarvestAsync_ShouldHandleNestedTypes()
    {
        // Arrange
        var code = @"
            namespace Test;
            public class Outer {
                /// <summary>Inner Summary</summary>
                public class Inner {}
            }
        ";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Nested.cs"), code);

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        // We expect "Outer.Inner" in Title (if logic supports it) or check Path which contains nesting
        Assert.Contains(results, n => n.Path.Contains("Outer-Inner") && n.Content.Contains("Inner Summary"));
    }

    [Fact]
    public async Task HarvestAsync_ShouldHandleExceptionDuringFileProcessing()
    {
        // Arrange
        // I'll try to create a file and then make it unreadable
        var filePath = Path.Combine(_testRoot, "Exception.cs");
        await File.WriteAllTextAsync(filePath, "docs");

        // On macOS/Linux, we can use chmod 000
        // File.SetUnixFileMode is available in .NET 7+
        File.SetUnixFileMode(filePath, UnixFileMode.None);

        // Act
        try
        {
            var results = await _harvester.HarvestAsync(_testRoot);

            // Assert
            // Should not throw, should just log and continue (empty in this case)
            Assert.Empty(results);
        }
        finally
        {
            // Restore permissions so it can be deleted during Dispose
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
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
