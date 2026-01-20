namespace ForgeTrust.Runnable.Web.RazorWire;

public class RazorWireOptions
{
    public static RazorWireOptions Default { get; } = new();

    public RazorWireStreamOptions Streams { get; } = new();

    public RazorWireCacheOptions Caching { get; } = new();
}

public class RazorWireStreamOptions
{
    public string BasePath { get; set; } = "/_rw/streams";
}

public class RazorWireCacheOptions
{
    public string PagePolicyName { get; set; } = "rw-page";
    public string IslandPolicyName { get; set; } = "rw-island";
}
