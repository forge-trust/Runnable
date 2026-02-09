# RazorWire Playwright Integration Tests

This project contains browser-level integration tests for the RazorWire MVC sample application in `examples/razorwire-mvc`.

## What it validates

- The sample app boots successfully.
- RazorWire stream connection is established.
- A message published from one browser session is received in another session via SSE.

## Run

```bash
dotnet test Web/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests.csproj
```

The fixture installs Playwright Chromium automatically on first run.
