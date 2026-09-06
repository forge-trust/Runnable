namespace ForgeTrust.AppSurface.Docs.Models;

/// <summary>
/// Selects the rendering contract for a Docs details page.
/// </summary>
/// <remarks>
/// This is deliberately an internal discriminator rather than a path-suffix convention. Namespace pages are
/// extensionless, and a public or derived harvester continues to use the legacy content route even when it harvests
/// C# source.
/// </remarks>
internal enum CSharpRenderKind
{
    /// <summary>
    /// The document is rendered through the established HTML-content path.
    /// </summary>
    Legacy = 0,

    /// <summary>
    /// The built-in C# namespace projection is rendered through constrained Razor partials.
    /// </summary>
    TypedNamespace = 1
}

/// <summary>
/// Immutable semantic projection for one built-in C# namespace page.
/// </summary>
/// <remarks>
/// The projection is attached only to the exact built-in <see cref="Services.CSharpDocHarvester"/> aggregation path.
/// It does not alter <see cref="DocNode"/>'s public positional API or create a renderer extension point.
/// </remarks>
internal sealed record CSharpNamespaceDocument(
    string FullNamespace,
    string Title,
    IReadOnlyList<CSharpChildNamespace> ChildNamespaces,
    IReadOnlyList<CSharpTypeDocument> Types,
    IReadOnlyList<CSharpEnumDocument> Enums,
    IReadOnlyList<DocOutlineItem> Outline,
    IReadOnlyList<DocSymbolSourceProvenance> SymbolSourceProvenance,
    string ReaderText,
    string? IntroHtml = null,
    IReadOnlyList<DocNamespaceEntryPoint>? EntryPoints = null);

/// <summary>
/// A child namespace link resolved from the built-in namespace hierarchy.
/// </summary>
internal sealed record CSharpChildNamespace(string Path, string Title);

/// <summary>
/// A documented C# type and its directly documented members.
/// </summary>
internal sealed record CSharpTypeDocument(
    string AnchorId,
    string DisplayName,
    CSharpDocumentation? Documentation,
    IReadOnlyList<CSharpMethodGroupDocument> MethodGroups,
    IReadOnlyList<CSharpPropertyDocument> Properties,
    string? SourceHref = null);

/// <summary>
/// A documented enum declaration.
/// </summary>
internal sealed record CSharpEnumDocument(
    string AnchorId,
    string DisplayName,
    CSharpDocumentation Documentation,
    string? SourceHref = null);

/// <summary>
/// A group of overloads with one stable group anchor.
/// </summary>
internal sealed record CSharpMethodGroupDocument(
    string AnchorId,
    string Name,
    IReadOnlyList<CSharpMethodDocument> Overloads);

/// <summary>
/// One documented method overload.
/// </summary>
internal sealed record CSharpMethodDocument(
    string AnchorId,
    CSharpSignature Signature,
    CSharpDocumentation Documentation,
    string? SourceHref = null);

/// <summary>
/// One documented property declaration.
/// </summary>
internal sealed record CSharpPropertyDocument(
    string AnchorId,
    string Name,
    CSharpSignature Signature,
    CSharpDocumentation Documentation,
    string? SourceHref = null);

/// <summary>
/// Safe display data for a C# declaration signature.
/// </summary>
internal sealed record CSharpSignature(
    string Type,
    string Name,
    IReadOnlyList<CSharpSignatureParameter> Parameters,
    IReadOnlyList<string> TypeParameters,
    string? ExplicitInterface = null,
    string? AccessorSignature = null);

/// <summary>
/// Safe display data for a method parameter.
/// </summary>
internal sealed record CSharpSignatureParameter(
    string? Modifier,
    string Type,
    string Name,
    string? DefaultValue = null);

/// <summary>
/// The semantic sections extracted from an XML documentation comment.
/// </summary>
internal sealed record CSharpDocumentation(IReadOnlyList<CSharpDocumentationSection> Sections);

/// <summary>
/// A named documentation section such as summary, parameter, or exception data.
/// </summary>
internal sealed record CSharpDocumentationSection(
    CSharpDocumentationSectionKind Kind,
    string? Name,
    string? CrefTarget,
    string? CrefDisplay,
    IReadOnlyList<CSharpXmlNode> Content);

/// <summary>
/// The supported XML documentation section categories.
/// </summary>
internal enum CSharpDocumentationSectionKind
{
    /// <summary>Summary documentation.</summary>
    Summary = 0,

    /// <summary>Type parameter documentation.</summary>
    TypeParameter = 1,

    /// <summary>Method or property parameter documentation.</summary>
    Parameter = 2,

    /// <summary>Return-value documentation.</summary>
    Returns = 3,

    /// <summary>Exception documentation.</summary>
    Exception = 4,

    /// <summary>Remarks documentation.</summary>
    Remarks = 5,

    /// <summary>Example documentation.</summary>
    Example = 6
}

/// <summary>
/// A safe XML documentation node. Leaf values are encoded by Razor; targets are retained separately from display text.
/// </summary>
internal sealed record CSharpXmlNode(
    CSharpXmlNodeKind Kind,
    string? Text = null,
    string? Target = null,
    IReadOnlyList<CSharpXmlNode>? Children = null,
    bool Ordered = false);

/// <summary>
/// The supported XML documentation node kinds.
/// </summary>
internal enum CSharpXmlNodeKind
{
    /// <summary>Plain text.</summary>
    Text = 0,

    /// <summary>Inline code.</summary>
    InlineCode = 1,

    /// <summary>Block code.</summary>
    CodeBlock = 2,

    /// <summary>A paragraph.</summary>
    Paragraph = 3,

    /// <summary>A list.</summary>
    List = 4,

    /// <summary>A list item.</summary>
    ListItem = 5,

    /// <summary>A C# XML <c>see</c> reference.</summary>
    Cref = 6,

    /// <summary>A parameter reference.</summary>
    ParameterReference = 7,

    /// <summary>A type-parameter reference.</summary>
    TypeParameterReference = 8
}
