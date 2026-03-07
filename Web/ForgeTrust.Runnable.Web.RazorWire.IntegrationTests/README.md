# RazorWire Playwright Integration Tests

This project contains browser-level integration tests for the RazorWire MVC sample application in `examples/razorwire-mvc`.
It also includes a RazorDocs browser regression test that runs against `Web/ForgeTrust.Runnable.Web.RazorDocs`.

## What it validates

- The sample app boots successfully.
- RazorWire stream connection is established.
- A message published from one browser session is received in another session via SSE.
- Reactivity message history persists when navigating away and back, and after full-page reload.
- Antiforgery behavior:
  - valid form submissions are accepted,
  - submissions without antiforgery token are rejected with `400`,
  - `RegisterTwoUsers_FromSingleSession_WithoutRefresh_AntiforgeryAllowsBothPosts` verifies antiforgery accepts both registration POSTs from a single session without refreshing the token.
- Increment counter behavior:
  - single-session increment updates instance/session/client-count values without refresh,
  - multi-session flow keeps session score independent while instance score is global,
  - session score persists while navigating across Home, Navigation, and Reactivity pages.
- RazorDocs search behavior:
  - sidebar search works from `/docs`,
  - navigating to `/docs/search` via Turbo keeps both advanced search and sidebar search functional.

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
