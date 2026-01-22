namespace ForgeTrust.Runnable.Web.RazorDocs.Models;

/// <summary>
/// Represents a documentation node within the repository.
/// </summary>
/// <param name="Title">The display title of the document.</param>
/// <param name="Path">The relative path to the documentation source.</param>
/// <param name="Content">The rendered HTML content of the documentation.</param>
/// <param name="ParentPath">The optional parent path for hierarchical organization.</param>
/// <param name="IsDirectory">Indicates if this node represents a directory container.</param>
public record DocNode(
    string Title,
    string Path,
    string Content,
    string? ParentPath = null,
    bool IsDirectory = false);

/// <summary>
/// Interface for harvesting documentation from various sources.
/// </summary>
public interface IDocHarvester
{
    /// <summary>
    /// Asynchronously scans the specified root path and returns a collection of documentation nodes harvested from sources under that path.
    /// </summary>
    /// <param name="rootPath">The filesystem root path to scan for documentation sources.</param>
    /// <summary>
/// Harvests documentation nodes from the specified filesystem root path.
/// </summary>
/// <param name="rootPath">The filesystem root path to scan for documentation sources.</param>
/// <returns>A collection of <see cref="DocNode"/> representing the harvested documentation.</returns>
    Task<IEnumerable<DocNode>> HarvestAsync(string rootPath);
}