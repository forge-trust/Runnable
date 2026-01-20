using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

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
        // With correct parent path (Types.cs)
        // Note: ID generation now includes namespace, so we don't assert exact path suffix here, just parent/title.
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

        // Expect sanitized ID including namespace (Test.SignatureTest.MyMethod...)
        // ToSafeId replaces dots with hyphens: Test-SignatureTest-MyMethod-int-string-ref-bool
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
        // Look for method nodes (Calculator is file, Calculator is type (suppressed matching file?), Methods are nodes)
        // Wait, if Calculator matches filename Calculator.cs (without ext), type node is suppressed.
        // So we only see File node + 2 Method nodes.
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
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // We expect ONE file node (Collision)
        // AND TWO type nodes (SharedName). 
        // Note: Filename "Collision" does NOT match "SharedName", so both are kept.
        var types = results.Where(n => n.Title == "SharedName").ToList();

        // Assert
        Assert.Equal(2, types.Count);
        Assert.NotEqual(types[0].Path, types[1].Path);

        // Verify qualified anchors in their PATHs (stubs)
        // One should be #NamespaceA-SharedName, other #NamespaceB-SharedName
        var pathA = types.Any(t => t.Path.EndsWith("#NamespaceA-SharedName"));
        var pathB = types.Any(t => t.Path.EndsWith("#NamespaceB-SharedName"));

        Assert.True(pathA, "Should contain anchor for NamespaceA.SharedName");
        Assert.True(pathB, "Should contain anchor for NamespaceB.SharedName");

        // Also verify the CONTENT in the file node has the correct IDs
        var fileNode = results.Single(n => n.Title == "Collision");
        Assert.Contains("id=\"NamespaceA-SharedName\"", fileNode.Content);
        Assert.Contains("id=\"NamespaceB-SharedName\"", fileNode.Content);
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

        // Type name "RemarksTest" != Filename "Remarks" -> Type node exists
        var node = results.Last(n => n.Title == "RemarksTest");
        // Note: The STUB node has empty content. The FILE node has the content.
        // This test from upstream checked 'node.Content' assuming node IS the type node with content.
        // In consolidated world, 'node' is a stub. We must check the FILE node.

        var fileNode = results.Single(n => n.Title == "Remarks");

        // Assert
        Assert.Contains("<div class='doc-summary", fileNode.Content);
        Assert.Contains("Summary", fileNode.Content);
        Assert.Contains("<div class='doc-remarks", fileNode.Content);
        Assert.Contains("Remarks here", fileNode.Content);
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
        var fileNode = results.Single(n => n.Title == "Nested");

        // Assert
        // Check content for inner summary
        Assert.Contains("Inner Summary", fileNode.Content);

        // Check for nested ID: Test.Outer.Inner -> Test-Outer-Inner
        Assert.Contains("id=\"Test-Outer-Inner\"", fileNode.Content);
    }

    [Fact]
    public async Task HarvestAsync_ShouldHandleExceptionDuringFileProcessing()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "Exception.cs");
        await File.WriteAllTextAsync(filePath, "docs");

        // On macOS/Linux, we can use chmod 000
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(filePath, UnixFileMode.None);
        }
        else
        {
            // On Windows, maybe lock the file? 
            // Skipping for simplicity or using FileShare.None if needed, but for now just relying on the upstream test's intent.
            // If we can't easily simulate exception on Windows without locking, we might skip logic.
            // But let's just assume we only test this on non-Windows or if possible.
            // Actually, if we can't verify exception, we just return.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        }

        // Act
        try
        {
            var results = await _harvester.HarvestAsync(_testRoot);

            // Assert
            Assert.Empty(results);
        }
        finally
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
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
