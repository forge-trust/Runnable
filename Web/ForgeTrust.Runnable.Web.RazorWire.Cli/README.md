# RazorWire CLI

The **RazorWire CLI** is a command-line tool for managing RazorWire projects. Its primary feature is the ability to export a reactive RazorWire site into a static or hybrid directory for hosting on services like S3 or traditional web servers.

## Installation

You can run the CLI directly using the `dotnet run` command from the project directory, or build it as a global tool.

```bash
dotnet run --project Web/ForgeTrust.Runnable.Web.RazorWire.Cli -- [command] [options]
```

## Commands


### `export`

Exports a RazorWire application to a static directory.

**Options:**
- **`-o|--output <path>`**: Output directory where the static files will be saved (default: `dist`).
- **`-r|--routes <path>`**: Optional path to a file containing seed routes to crawl.
- **`-u|--url <url>`**: Base URL of a running application used for crawling.
- **`-p|--project <path.csproj>`**: Path to a .NET project to run automatically and export.
- **`-d|--dll <path.dll>`**: Path to a .NET DLL to run automatically and export.
- **`--app-args <token>`**: Repeatable app-argument token to pass through when launching `--project` or `--dll`.
- **`--no-build`**: Project mode only. Skips the release build step and reuses existing `bin/Release` output.

Exactly one source option is required: `--url`, `--project`, or `--dll`.

When `--project` or `--dll` is used:
- Project mode builds release output, resolves the built app DLL, then launches that DLL for crawling.
- Project mode builds by default; add `--no-build` only when you want to skip build and reuse existing `bin/Release` output.
- Launched app processes run in production environment (`DOTNET_ENVIRONMENT=Production`, `ASPNETCORE_ENVIRONMENT=Production`).
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
