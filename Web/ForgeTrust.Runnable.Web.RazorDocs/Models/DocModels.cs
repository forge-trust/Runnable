namespace ForgeTrust.Runnable.Web.RazorDocs.Models;

public record DocNode(string Title, string Path, string Content, string? ParentPath = null, bool IsDirectory = false);

public interface IDocHarvester
{
    Task<IEnumerable<DocNode>> HarvestAsync(string rootPath);
}
