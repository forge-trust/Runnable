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
- **`-p|--project <path>`** (Required): Path to the project file to export.
- **`-o|--output <path>`**: Output directory where the static files will be saved (default: `dist`).
- **`-m|--mode <s3|hybrid>`**: Export mode.
  - `s3`: Full static export suitable for S3/CDN hosting.
  - `hybrid` (default): Static assets with support for dynamic Islands.
- **`-r|--routes <path>`**: Optional path to a file containing seed routes to crawl.
- **`-u|--url <url>`**: The base URL of the running application used for crawling (default: `http://localhost:5000`).

**Example:**

```bash
dotnet run --project Web/ForgeTrust.Runnable.Web.RazorWire.Cli -- export -p MyWebApp.csproj -o ./dist --mode hybrid
```
