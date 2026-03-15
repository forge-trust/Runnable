namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

internal sealed record TailwindExecutableRequest(
    string Version,
    string? ExecutablePath,
    string? InstallDirectory);
