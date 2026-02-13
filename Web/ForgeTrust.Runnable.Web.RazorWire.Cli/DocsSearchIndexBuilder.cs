using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// Builds docs search artifacts for static exports.
/// </summary>
public sealed class DocsSearchIndexBuilder
{
    private static readonly Regex ScriptOrStyleRegex = new(
        "<(script|style)[^>]*>.*?</\\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex H1Regex = new(
        "<h1[^>]*>(.*?)</h1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex H2H3Regex = new(
        "<h[23][^>]*>(.*?)</h[23]>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TitleRegex = new(
        "<title[^>]*>(.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ParagraphRegex = new(
        "<(p|li|blockquote)[^>]*>(.*?)</\\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MultiSpaceRegex = new(
        "\\s+",
        RegexOptions.Compiled);

    private const string DefaultCdnUrl =
        "https://cdn.jsdelivr.net/npm/minisearch@7.1.2/dist/umd/index.min.js";

    private readonly ILogger<DocsSearchIndexBuilder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocsSearchIndexBuilder"/> class.
    /// </summary>
    /// <param name="logger">Logger used for artifact generation diagnostics.</param>
    public DocsSearchIndexBuilder(ILogger<DocsSearchIndexBuilder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates docs search artifacts from exported docs HTML pages.
    /// </summary>
    public async Task GenerateArtifactsAsync(
        ExportContext context,
        IReadOnlyDictionary<string, string> docsHtmlByRoute,
        CancellationToken cancellationToken = default)
    {
        if (!context.DocsSearchEnabled)
        {
            return;
        }

        var docsDir = Path.Combine(context.OutputPath, "docs");
        Directory.CreateDirectory(docsDir);

        var records = BuildRecords(docsHtmlByRoute);
        var index = new DocSearchIndexDocument(
            new DocSearchIndexMetadata(DateTimeOffset.UtcNow.ToString("O"), "1", "minisearch"),
            records);

        var json = JsonSerializer.Serialize(
            index,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

        var indexPath = Path.Combine(docsDir, "search-index.json");
        await File.WriteAllTextAsync(indexPath, json, cancellationToken);

        var cssPath = Path.Combine(docsDir, "search.css");
        await File.WriteAllTextAsync(cssPath, SearchCss, cancellationToken);

        var searchClientPath = Path.Combine(docsDir, "search-client.js");
        var useCdn = string.Equals(context.SearchRuntime, "cdn", StringComparison.OrdinalIgnoreCase);
        var cdnUrl = string.IsNullOrWhiteSpace(context.SearchCdnUrl) ? DefaultCdnUrl : context.SearchCdnUrl!;
        await File.WriteAllTextAsync(searchClientPath, BuildSearchClient(useCdn, cdnUrl), cancellationToken);

        var miniSearchPath = Path.Combine(docsDir, "minisearch.min.js");
        if (!useCdn)
        {
            await File.WriteAllTextAsync(miniSearchPath, MiniSearchLocalRuntime, cancellationToken);
        }
        else if (File.Exists(miniSearchPath))
        {
            File.Delete(miniSearchPath);
        }

        _logger.LogInformation(
            "Generated docs search artifacts: {RecordCount} docs, runtime={Runtime}",
            records.Count,
            useCdn ? "cdn" : "local");
    }

    internal IReadOnlyList<DocSearchRecord> BuildRecords(IReadOnlyDictionary<string, string> docsHtmlByRoute)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var records = new List<DocSearchRecord>();

        foreach (var kvp in docsHtmlByRoute.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var route = kvp.Key;
            var html = kvp.Value ?? string.Empty;

            if (!route.StartsWith("/docs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (route.Equals("/docs/search", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(route))
            {
                continue;
            }

            var title = ExtractTitle(html, route);
            var headings = ExtractHeadings(html);
            var bodyText = ExtractBodyText(html);
            var snippet = ExtractSnippet(html, bodyText);

            var isDocsRoot = route.Equals("/docs", StringComparison.OrdinalIgnoreCase)
                             || route.Equals("/docs/", StringComparison.OrdinalIgnoreCase);

            if (isDocsRoot && string.IsNullOrWhiteSpace(bodyText))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(bodyText))
            {
                continue;
            }

            records.Add(
                new DocSearchRecord(
                    Id: route,
                    Path: route,
                    Title: title,
                    Headings: headings,
                    BodyText: bodyText,
                    Snippet: snippet));
        }

        return records;
    }

    private static string ExtractTitle(string html, string route)
    {
        var h1 = ExtractFirstMatch(H1Regex, html);
        if (!string.IsNullOrWhiteSpace(h1))
        {
            return h1;
        }

        var title = ExtractFirstMatch(TitleRegex, html);
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        var trimmed = route.Trim('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "Documentation";
        }

        var last = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(last))
        {
            return "Documentation";
        }

        return WebUtility.HtmlDecode(last.Replace('-', ' '));
    }

    private static IReadOnlyList<string> ExtractHeadings(string html)
    {
        return H2H3Regex.Matches(html)
            .Select(m => ToPlainText(m.Groups[1].Value))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();
    }

    private static string ExtractBodyText(string html)
    {
        var noScriptStyle = ScriptOrStyleRegex.Replace(html, string.Empty);
        var withoutTags = TagRegex.Replace(noScriptStyle, " ");
        return NormalizeText(withoutTags);
    }

    private static string ExtractSnippet(string html, string bodyText)
    {
        foreach (Match match in ParagraphRegex.Matches(html))
        {
            var text = ToPlainText(match.Groups[2].Value);
            if (text.Length >= 20)
            {
                return Truncate(text, 220);
            }
        }

        return Truncate(bodyText, 220);
    }

    private static string ExtractFirstMatch(Regex regex, string html)
    {
        var match = regex.Match(html);
        return match.Success ? ToPlainText(match.Groups[1].Value) : string.Empty;
    }

    private static string ToPlainText(string htmlFragment)
    {
        return NormalizeText(TagRegex.Replace(htmlFragment, " "));
    }

    private static string NormalizeText(string text)
    {
        var decoded = WebUtility.HtmlDecode(text ?? string.Empty);
        return MultiSpaceRegex.Replace(decoded, " ").Trim();
    }

    private static string Truncate(string text, int maxLen)
    {
        if (text.Length <= maxLen)
        {
            return text;
        }

        return text[..maxLen].TrimEnd() + "...";
    }

    private static string BuildSearchClient(bool useCdn, string cdnUrl)
    {
        var escapedCdn = JsonSerializer.Serialize(cdnUrl);
        var escapedRuntime = JsonSerializer.Serialize(useCdn ? "cdn" : "local");

        return $$"""
                 (() => {
                   const runtimeMode = {{escapedRuntime}};
                   const cdnUrl = {{escapedCdn}};
                   const indexUrl = '/docs/search-index.json';
                   const topResults = 8;
                   let searchIndex = null;
                   let docs = [];
                 
                   const sidebarInput = document.getElementById('docs-search-input');
                   const sidebarResults = document.getElementById('docs-search-results');
                   const sidebarStatus = document.getElementById('docs-search-status');
                   const pageRoot = document.getElementById('docs-search-page');
                   const pageInput = document.getElementById('docs-search-page-input');
                   const pageResults = document.getElementById('docs-search-page-results');
                   const pageStatus = document.getElementById('docs-search-page-status');
                 
                   function debounce(fn, delay) {
                     let timer = null;
                     return (...args) => {
                       window.clearTimeout(timer);
                       timer = window.setTimeout(() => fn(...args), delay);
                     };
                   }
                 
                   function escapeHtml(value) {
                     return String(value ?? '')
                       .replaceAll('&', '&amp;')
                       .replaceAll('<', '&lt;')
                       .replaceAll('>', '&gt;')
                       .replaceAll('"', '&quot;')
                       .replaceAll("'", '&#39;');
                   }
                 
                   async function ensureMiniSearch() {
                     if (window.MiniSearch) {
                       return;
                     }
                 
                     if (runtimeMode === 'cdn' && cdnUrl) {
                       await loadScript(cdnUrl);
                     } else {
                       await loadScript('/docs/minisearch.min.js');
                     }
                   }
                 
                   function loadScript(src) {
                     return new Promise((resolve, reject) => {
                       const script = document.createElement('script');
                       script.src = src;
                       script.async = true;
                       script.onload = () => resolve();
                       script.onerror = () => reject(new Error(`Failed loading script: ${src}`));
                       document.head.appendChild(script);
                     });
                   }
                 
                   async function init() {
                     try {
                       await ensureMiniSearch();
                       await loadIndex();
                       bindSidebar();
                       bindSearchPage();
                     } catch (err) {
                       console.error(err);
                       setStatus(sidebarStatus, 'Search is temporarily unavailable.');
                       setStatus(pageStatus, 'Search is temporarily unavailable.');
                     }
                   }
                 
                   async function loadIndex() {
                     const response = await fetch(indexUrl, { credentials: 'same-origin' });
                     if (!response.ok) {
                       throw new Error(`Failed to load search index: ${response.status}`);
                     }
                 
                     const payload = await response.json();
                     docs = Array.isArray(payload.documents) ? payload.documents : [];
                 
                     const MiniSearch = window.MiniSearch;
                     searchIndex = new MiniSearch({
                       fields: ['title', 'headings', 'bodyText'],
                       storeFields: ['id', 'path', 'title', 'snippet'],
                       searchOptions: {
                         prefix: true,
                         fuzzy: 0.1,
                         boost: { title: 6, headings: 3, bodyText: 1 }
                       }
                     });
                 
                     searchIndex.addAll(docs.map((d) => ({
                       id: d.id,
                       path: d.path,
                       title: d.title,
                       headings: Array.isArray(d.headings) ? d.headings.join(' ') : '',
                       bodyText: d.bodyText ?? '',
                       snippet: d.snippet ?? ''
                     })));
                   }
                 
                   function query(q, max = topResults) {
                     if (!searchIndex || !q || !q.trim()) {
                       return [];
                     }
                 
                     return searchIndex.search(q.trim(), {
                       prefix: true,
                       fuzzy: 0.1,
                       boost: { title: 6, headings: 3, bodyText: 1 }
                     }).slice(0, max);
                   }
                 
                   function bindSidebar() {
                     if (!sidebarInput || !sidebarResults) {
                       return;
                     }
                 
                     let activeIndex = -1;
                     const runSearch = debounce(() => {
                       activeIndex = -1;
                       const q = sidebarInput.value;
                       const results = query(q, topResults);
                       renderSidebarResults(results, q);
                     }, 120);
                 
                     sidebarInput.addEventListener('input', runSearch);
                     sidebarInput.addEventListener('keydown', (event) => {
                       const items = Array.from(sidebarResults.querySelectorAll('[role="option"]'));
                       if (!items.length) {
                         return;
                       }
                 
                       if (event.key === 'ArrowDown') {
                         event.preventDefault();
                         activeIndex = Math.min(activeIndex + 1, items.length - 1);
                         setActiveSidebarOption(items, activeIndex);
                       } else if (event.key === 'ArrowUp') {
                         event.preventDefault();
                         activeIndex = Math.max(activeIndex - 1, 0);
                         setActiveSidebarOption(items, activeIndex);
                       } else if (event.key === 'Enter') {
                         if (activeIndex >= 0 && items[activeIndex]) {
                           event.preventDefault();
                           const href = items[activeIndex].getAttribute('data-href');
                           if (href) {
                             window.location.assign(href);
                           }
                         }
                       } else if (event.key === 'Escape') {
                         sidebarResults.innerHTML = '';
                         sidebarResults.classList.add('hidden');
                       }
                     });
                   }
                 
                   function bindSearchPage() {
                     if (!pageRoot || !pageInput || !pageResults) {
                       return;
                     }
                 
                     const params = new URLSearchParams(window.location.search);
                     const initialQuery = params.get('q') ?? '';
                     pageInput.value = initialQuery;
                     renderSearchPageResults(initialQuery);
                 
                     const onInput = debounce(() => {
                       const q = pageInput.value.trim();
                       const url = new URL(window.location.href);
                       if (q) {
                         url.searchParams.set('q', q);
                       } else {
                         url.searchParams.delete('q');
                       }
                 
                       window.history.replaceState({}, '', url);
                       renderSearchPageResults(q);
                     }, 120);
                 
                     pageInput.addEventListener('input', onInput);
                   }
                 
                   function renderSidebarResults(results, q) {
                     if (!sidebarResults) {
                       return;
                     }
                 
                     if (!q || !q.trim()) {
                       sidebarResults.innerHTML = '';
                       sidebarResults.classList.add('hidden');
                       setStatus(sidebarStatus, '');
                       return;
                     }
                 
                     if (!results.length) {
                       sidebarResults.classList.remove('hidden');
                       sidebarResults.innerHTML = '<li class="docs-search-empty" role="option">No matching docs found.</li>';
                       setStatus(sidebarStatus, 'No matching docs found.');
                       return;
                     }
                 
                     sidebarResults.classList.remove('hidden');
                     sidebarResults.innerHTML = results.map((item, index) => {
                       const selected = index === 0 ? 'true' : 'false';
                       return `<li role="option" aria-selected="${selected}" tabindex="-1" class="docs-search-option" data-href="${escapeHtml(item.path)}">
                         <a href="${escapeHtml(item.path)}" data-turbo-frame="doc-content" data-turbo-action="advance">
                           <span class="docs-search-option-title">${escapeHtml(item.title)}</span>
                           <span class="docs-search-option-path">${escapeHtml(item.path)}</span>
                         </a>
                       </li>`;
                     }).join('');
                 
                     setStatus(sidebarStatus, `${results.length} result(s).`);
                   }
                 
                   function setActiveSidebarOption(items, activeIndex) {
                     items.forEach((item, i) => {
                       const selected = i === activeIndex;
                       item.setAttribute('aria-selected', selected ? 'true' : 'false');
                       item.classList.toggle('active', selected);
                     });
                   }
                 
                   function renderSearchPageResults(q) {
                     if (!pageResults) {
                       return;
                     }
                 
                     if (!q || !q.trim()) {
                       setStatus(pageStatus, 'Type to search across documentation.');
                       pageResults.innerHTML = '';
                       return;
                     }
                 
                     const results = query(q, 100);
                     setStatus(pageStatus, `${results.length} result(s) for "${q}".`);
                 
                     if (!results.length) {
                       pageResults.innerHTML = '<p class="docs-search-empty">No results found.</p>';
                       return;
                     }
                 
                     pageResults.innerHTML = results.map((item) => `
                       <article class="docs-search-result">
                         <h2><a href="${escapeHtml(item.path)}">${escapeHtml(item.title)}</a></h2>
                         <p class="docs-search-result-path">${escapeHtml(item.path)}</p>
                         <p class="docs-search-result-snippet">${escapeHtml(item.snippet || '')}</p>
                       </article>
                     `).join('');
                   }
                 
                   function setStatus(node, text) {
                     if (node) {
                       node.textContent = text;
                     }
                   }
                 
                   init();
                 })();
                 """;
    }

    internal const string SearchCss = """
                                     #docs-search-shell {
                                         margin-bottom: 0.75rem;
                                     }

                                     #docs-search-input,
                                     #docs-search-page-input {
                                         width: 100%;
                                         border: 1px solid #334155;
                                         background: #020617;
                                         color: #e2e8f0;
                                         border-radius: 0.5rem;
                                         padding: 0.55rem 0.75rem;
                                         outline: none;
                                     }

                                     #docs-search-input:focus,
                                     #docs-search-page-input:focus {
                                         border-color: #22d3ee;
                                         box-shadow: 0 0 0 1px #22d3ee inset;
                                     }

                                     #docs-search-results {
                                         margin-top: 0.5rem;
                                         border: 1px solid #1e293b;
                                         border-radius: 0.5rem;
                                         background: #020617;
                                         overflow: hidden;
                                         max-height: 18rem;
                                         overflow-y: auto;
                                     }

                                     #docs-search-results.hidden {
                                         display: none;
                                     }

                                     .docs-search-option a {
                                         display: block;
                                         padding: 0.55rem 0.65rem;
                                         text-decoration: none;
                                     }

                                     .docs-search-option-title {
                                         display: block;
                                         color: #e2e8f0;
                                         font-size: 0.88rem;
                                     }

                                     .docs-search-option-path {
                                         display: block;
                                         color: #64748b;
                                         font-size: 0.7rem;
                                     }

                                     .docs-search-option.active,
                                     .docs-search-option:hover {
                                         background: #0f172a;
                                     }

                                     .docs-search-empty {
                                         color: #94a3b8;
                                         font-size: 0.8rem;
                                         padding: 0.65rem;
                                     }

                                     .docs-search-page {
                                         max-width: 52rem;
                                         margin: 0 auto;
                                     }

                                     .docs-search-page-input-wrap {
                                         margin-bottom: 1rem;
                                     }

                                     .docs-search-page-status {
                                         color: #94a3b8;
                                         margin-bottom: 1rem;
                                         font-size: 0.9rem;
                                     }

                                     .docs-search-result {
                                         border: 1px solid #1e293b;
                                         border-radius: 0.75rem;
                                         padding: 1rem;
                                         margin-bottom: 0.75rem;
                                         background: #020617;
                                     }

                                     .docs-search-result h2 {
                                         margin: 0;
                                         font-size: 1.05rem;
                                     }

                                     .docs-search-result h2 a {
                                         color: #67e8f9;
                                         text-decoration: none;
                                     }

                                     .docs-search-result h2 a:hover {
                                         text-decoration: underline;
                                     }

                                     .docs-search-result-path {
                                         color: #64748b;
                                         font-size: 0.8rem;
                                         margin: 0.35rem 0;
                                     }

                                     .docs-search-result-snippet {
                                         color: #cbd5e1;
                                         margin: 0;
                                         line-height: 1.4;
                                     }
                                     """;

    internal const string MiniSearchLocalRuntime = """
                                                   (function (global) {
                                                     function tokenize(value) {
                                                       return String(value || '')
                                                         .toLowerCase()
                                                         .split(/[^a-z0-9]+/g)
                                                         .filter(Boolean);
                                                     }
                                                   
                                                     function scoreDoc(doc, queryTokens, boost) {
                                                       let score = 0;
                                                       for (const token of queryTokens) {
                                                         if (doc._tokens.title.some((t) => t.startsWith(token))) score += boost.title || 1;
                                                         if (doc._tokens.headings.some((t) => t.startsWith(token))) score += boost.headings || 1;
                                                         if (doc._tokens.bodyText.some((t) => t.startsWith(token))) score += boost.bodyText || 1;
                                                       }
                                                       return score;
                                                     }
                                                   
                                                     function MiniSearch(options) {
                                                       this.options = options || {};
                                                       this.docs = [];
                                                     }
                                                   
                                                     MiniSearch.prototype.addAll = function (documents) {
                                                       for (const doc of documents || []) {
                                                         this.docs.push({
                                                           id: doc.id,
                                                           path: doc.path,
                                                           title: doc.title,
                                                           snippet: doc.snippet,
                                                           _tokens: {
                                                             title: tokenize(doc.title),
                                                             headings: tokenize(doc.headings),
                                                             bodyText: tokenize(doc.bodyText)
                                                           }
                                                         });
                                                       }
                                                     };
                                                   
                                                     MiniSearch.prototype.search = function (query, opts) {
                                                       const tokens = tokenize(query);
                                                       if (!tokens.length) return [];
                                                       const boost = (opts && opts.boost) || { title: 1, headings: 1, bodyText: 1 };
                                                   
                                                       return this.docs
                                                         .map((doc) => ({ doc, score: scoreDoc(doc, tokens, boost) }))
                                                         .filter((r) => r.score > 0)
                                                         .sort((a, b) => b.score - a.score)
                                                         .map((r) => ({
                                                           id: r.doc.id,
                                                           path: r.doc.path,
                                                           title: r.doc.title,
                                                           snippet: r.doc.snippet,
                                                           score: r.score
                                                         }));
                                                     };
                                                   
                                                     global.MiniSearch = MiniSearch;
                                                   })(window);
                                                   """;
}
