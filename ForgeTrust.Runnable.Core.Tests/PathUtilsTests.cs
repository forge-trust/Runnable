using Xunit;

namespace ForgeTrust.Runnable.Core.Tests;

public class PathUtilsTests : IDisposable
{
    private readonly string _testRoot;

    public PathUtilsTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "PathUtilsTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void FindRepositoryRoot_ShouldFindRootWithGitFolder()
    {
        // Arrange
        var repoRoot = Path.Combine(_testRoot, "repo");
        var gitDir = Path.Combine(repoRoot, ".git");
        var nestedDir = Path.Combine(repoRoot, "src", "sub");
        
        Directory.CreateDirectory(gitDir);
        Directory.CreateDirectory(nestedDir);

        // Act
        var result = PathUtils.FindRepositoryRoot(nestedDir);

        // Assert
        Assert.Equal(repoRoot, result);
    }

    [Fact]
    public void FindRepositoryRoot_ShouldReturnStartPath_WhenNoGitFolderFound()
    {
        // Arrange
        var someDir = Path.Combine(_testRoot, "no-git", "sub");
        Directory.CreateDirectory(someDir);

        // Act
        var result = PathUtils.FindRepositoryRoot(someDir);

        // Assert
        Assert.Equal(someDir, result);
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
                // Best effort
            }
        }
    }
}
