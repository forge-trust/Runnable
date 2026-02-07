namespace RazorWireWebExample.Models;

/// <summary>
/// Represents a chat message item to be rendered in the UI.
/// </summary>
/// <param name="DisplayName">The display name of the user who sent the message.</param>
/// <param name="UtcTime">The UTC timestamp of the message, formatted as a string.</param>
/// <param name="Message">The content of the message.</param>
public record MessageItemModel(string DisplayName, string UtcTime, string Message);
