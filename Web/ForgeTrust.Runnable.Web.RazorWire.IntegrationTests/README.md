# RazorWire Playwright Integration Tests

This project contains browser-level integration tests for the RazorWire MVC sample application in `examples/razorwire-mvc`.

## What it validates

- The sample app boots successfully.
- RazorWire stream connection is established.
- A message published from one browser session is received in another session via SSE.
- Antiforgery behavior:
  - valid form submissions are accepted,
  - submissions without antiforgery token are rejected with `400`,
  - `RegisterTwoUsers_FromSingleSession_WithoutRefresh_AntiforgeryAllowsBothPosts` verifies antiforgery accepts both registration POSTs from a single session without refreshing the token.
- Increment counter behavior:
  - single-session increment updates instance/session/client-count values without refresh,
  - multi-session flow keeps session score independent while instance score is global,
  - session score persists while navigating across Home, Navigation, and Reactivity pages.

## Run

```bash
dotnet test Web/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests.csproj
```

The fixture installs Playwright Chromium automatically on first run.

### Run specific subsets

```bash
dotnet test Web/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests.csproj --filter "FullyQualifiedName~IncrementCounter"
```

```bash
dotnet test Web/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests.csproj --filter "Category=Integration"
```
