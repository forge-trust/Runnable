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
- **`-u|--url <url>`**: The base URL of the running application used for crawling (default: `http://localhost:5000`).
- **`--docs-search <bool>`**: Enable docs search artifact generation (default: `true`).
- **`--search-runtime <local|cdn>`**: Choose docs search runtime delivery mode (default: `local`).
- **`--search-cdn-url <url>`**: Optional CDN URL used when `--search-runtime cdn`.

**Example:**

```bash
dotnet run --project Web/ForgeTrust.Runnable.Web.RazorWire.Cli -- export -o ./dist -u http://localhost:5233
```
