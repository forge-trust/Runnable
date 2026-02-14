using CliFx.Exceptions;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class ExportSourceRequestFactoryTests
{
    private readonly ExportSourceRequestFactory _sut = new();

    [Fact]
    public void Create_Should_Throw_When_No_Source_Is_Provided()
    {
        Assert.Throws<CommandException>(() => _sut.Create(null, null, null, [], false));
    }

    [Fact]
    public void Create_Should_Throw_When_Multiple_Sources_Are_Provided()
    {
        Assert.Throws<CommandException>(() => _sut.Create("http://localhost:5000", "app.csproj", null, [], false));
    }

    [Fact]
    public void Create_Should_Throw_When_Url_Is_Invalid()
    {
        Assert.Throws<CommandException>(() => _sut.Create("not-a-url", null, null, [], false));
    }

    [Fact]
    public void Create_Should_Throw_When_Project_File_Is_Missing()
    {
        Assert.Throws<CommandException>(() => _sut.Create(null, "missing.csproj", null, [], false));
    }

    [Fact]
    public void Create_Should_Throw_When_Dll_File_Is_Missing()
    {
        Assert.Throws<CommandException>(() => _sut.Create(null, null, "missing.dll", [], false));
    }

    [Fact]
    public void Create_Should_Create_Project_Request_When_Project_Path_Exists()
    {
        var tempPath = Path.GetTempFileName();
        var projectPath = Path.ChangeExtension(tempPath, ".csproj");
        File.Move(tempPath, projectPath);
        try
        {
            var request = _sut.Create(null, projectPath, null, ["--foo", "bar"], true);

            Assert.Equal(ExportSourceKind.Project, request.SourceKind);
            Assert.Equal(Path.GetFullPath(projectPath), request.SourceValue);
            Assert.Equal(["--foo", "bar"], request.AppArgs);
            Assert.True(request.NoBuild);
        }
        finally
        {
            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
            }
        }
    }

    [Fact]
    public void Create_Should_Create_Dll_Request_When_Dll_Path_Exists()
    {
        var tempPath = Path.GetTempFileName();
        var dllPath = Path.ChangeExtension(tempPath, ".dll");
        File.Move(tempPath, dllPath);
        try
        {
            var request = _sut.Create(null, null, dllPath, ["--foo", "bar"], false);

            Assert.Equal(ExportSourceKind.Dll, request.SourceKind);
            Assert.Equal(Path.GetFullPath(dllPath), request.SourceValue);
            Assert.Equal(["--foo", "bar"], request.AppArgs);
            Assert.False(request.NoBuild);
        }
        finally
        {
            if (File.Exists(dllPath))
            {
                File.Delete(dllPath);
            }
        }
    }
}
