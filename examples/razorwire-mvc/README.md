# RazorWire MVC Example

This project is a reference implementation demonstrating how to build modern, reactive web applications using **RazorWire** within an ASP.NET Core MVC architecture.

It showcases the core "HTML-over-the-wire" capabilities provided by the `ForgeTrust.Runnable.Web.RazorWire` library, including Turbo Frames (Islands), Turbo Streams, and Server-Sent Events (SSE).

## Features

#### 🏝️ Islands (Turbo Frames)

RazorWire allows you to isolate regions of a page ("Islands") that can load or update independently.
*   **Example**: The Sidebar and User List in the Reactivity demo are loaded as separate frames (`<turbo-frame>`).
*   **Code**: See `ReactivityController.Sidebar()` and `RazorWireBridge.Frame()`.

### 📡 Real-time Streaming (SSE)

Updates can be pushed from the server to connected clients via Server-Sent Events.
*   **Example**: When a user posts a message or joins, the "User List" and "Messages" sections update in real-time for all connected clients.
*   **Hub**: The `IRazorWireStreamHub` is used to publish updates to the `reactivity` channel.

### ⚡ Form Enhancement

Forms are enhanced to perform partial page updates without full reloads.
*   **Example**: The "Join" and "Send Message" forms return Turbo Stream responses (appending messages, updating counts) instead of redirecting.
*   **Code**: See `ReactivityController.RegisterUser` and usage of `this.RazorWireStream()`.

## Project Structure

*   **Controllers/ReactivityController.cs**: The main demo controller. It handles:
  *   Rendering the main view.
  *   Serving partial "Islands" (Sidebar, UserList).
  *   Handling form POSTs and returning Stream responses.
  *   Broadcasting updates via the Stream Hub.
*   **Views/Reactivity/**: Contains the Razor views and partials for the demo.
*   **Services/**: Simple in-memory services (`UserPresenceService`) to simulate state for the demo.

## Getting Started

1.  **Run the application**:

    ```bash
    dotnet run
    ```

2.  **Open the demo**:
    Navigate to `http://localhost:5233` (or the port indicated in the console).
3.  **Test Reactivity**:
    *   Open the app in multiple browser tabs.
    *   Join with a username in one tab.
    *   Observe the "Online Users" count update instantly in other tabs.
    *   Send a message to see it appear everywhere.

## Key Concepts

**RazorWire Bridge**
Helper methods to return Turbo-compatible responses.
```csharp
// Return a Frame (Island)
return RazorWireBridge.Frame(this, "my-island-id", "_PartialViewName");

// Return a Stream (Update)
return this.RazorWireStream()
    .Append("chat-list", "<li>New Message</li>")
    .BuildResult();
```

**Stream Hub**
Publishing updates to subscribers.
```csharp
await _hub.PublishAsync("channel-name", streamHtml);
```
