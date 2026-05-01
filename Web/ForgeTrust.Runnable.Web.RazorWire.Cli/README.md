# RazorWire CLI

The **RazorWire CLI** is a command-line tool for managing RazorWire projects. Its primary feature is the ability to export a reactive RazorWire site into a static or hybrid directory for hosting on services like S3 or traditional web servers.

The CLI uses Runnable's command-first console mode. That means help and validation output are intentionally quiet, without Generic Host lifecycle banners, while real export runs still emit useful progress logs.

## Installation

You can run the CLI directly using the `dotnet run` command from the project directory, or build it as a global tool.

```bash
dotnet run --project Web/ForgeTrust.Runnable.Web.RazorWire.Cli -- [command] [options]
```

## Commands

### Help and validation behavior

- Root help (`--help`) and command help (`export --help`) are command-first by design.
- Validation failures, such as invalid flags or missing source options, should surface actionable CLI output without host startup and shutdown chatter.
- Successful export runs still keep command-owned progress output so long-running work remains understandable.

### `export`

Exports a RazorWire application to a static directory.

**Options:**
- **`-o|--output <path>`**: Output directory where the static files will be saved (default: `dist`).
- **`-r|--seeds <path>`**: Optional path to a file containing seed routes to crawl.
- **`-u|--url <url>`**: Base URL of a running application used for crawling.
- **`-p|--project <path.csproj>`**: Path to a .NET project to run automatically and export.
- **`-d|--dll <path.dll>`**: Path to a .NET DLL to run automatically and export.
- **`--app-args <token>`**: Repeatable app-argument token to pass through when launching `--project` or `--dll`.
- **`--no-build`**: Project mode only. Skips the release publish step and reuses existing published output.

Exactly one source option is required: `--url`, `--project`, or `--dll`.

When launched app processes are started by the CLI (`--project` or `--dll`), they run in production environment (`DOTNET_ENVIRONMENT=Production`, `ASPNETCORE_ENVIRONMENT=Production`).

When `--project` is used:
- Project mode publishes a release build by default.
- Project mode resolves the published app DLL and launches that DLL for crawling.
- Add `--no-build` to skip publishing and reuse existing published output.

When `--dll` is used:
- The CLI launches the provided DLL directly (no build or DLL resolution step).

For both `--project` and `--dll`:
- If you do not pass `--urls` via `--app-args`, the CLI appends `--urls http://127.0.0.1:0`.
- The CLI waits for startup, crawls the app, then shuts the process down automatically.

**Example:**

```bash
dotnet run --project Web/ForgeTrust.Runnable.Web.RazorWire.Cli -- export -o ./dist -u http://localhost:5233
```

```bash
dotnet run --project Web/ForgeTrust.Runnable.Web.RazorWire.Cli -- export -o ./dist -p ./examples/razorwire-mvc/RazorWireWebExample.csproj
```

```bash
dotnet run --project Web/ForgeTrust.Runnable.Web.RazorWire.Cli -- export -o ./dist -d ./bin/Release/net10.0/MyApp.dll --app-args --urls --app-args http://127.0.0.1:5009
```
