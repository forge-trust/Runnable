using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

public class TurboStreamResult : ContentResult
{
    public TurboStreamResult(string content)
    {
        Content = content;
        ContentType = "text/vnd.turbo-stream.html";
    }
}
