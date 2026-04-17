using System.Threading.Tasks;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

internal interface ICommandExecutor
{
    Task<ProcessResult> ExecuteCommandAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken);
}
