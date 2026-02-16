(() => {
  const indexUrl = '/docs/search-index.json';
  const maxQueryLength = 500;
  const topResults = 8;
  const fetchTimeoutMs = 10000;
  const docsFrameId = 'doc-content';
  const enableDocsPartialRewrite = Boolean(
    document.querySelector('meta[name="rw-docs-static-partials"][content="1"]')
  );
  const defaultSearchOptions = {
    prefix: true,
    fuzzy: 0.1,
    boost: { title: 6, headings: 3, bodyText: 1 }
  };
  let searchIndex = null;
  let searchIndexLoadPromise = null;
  const sidebarBoundAttribute = 'data-rw-search-sidebar-bound';
  const searchPageBoundAttribute = 'data-rw-search-page-bound';

  function getSidebarSearchElements() {
    return {
      input: document.getElementById('docs-search-input'),
      results: document.getElementById('docs-search-results'),
      status: document.getElementById('docs-search-status')
    };
  }

  function getSearchPageElements() {
    return {
      root: document.getElementById('docs-search-page'),
      input: document.getElementById('docs-search-page-input'),
      results: document.getElementById('docs-search-page-results'),
      status: document.getElementById('docs-search-page-status')
    };
  }

  function toUrl(urlLike) {
    if (!urlLike) {
      return null;
    }

    try {
      if (urlLike instanceof URL) {
        return new URL(urlLike.toString());
      }

      return new URL(String(urlLike), window.location.href);
    } catch {
      return null;
    }
  }

  function isDocsPath(path) {
    return path === '/docs' || path.startsWith('/docs/');
  }

  function getHeader(headers, name) {
    if (!headers) {
      return null;
    }

    if (headers instanceof Headers) {
      return headers.get(name);
    }

    if (typeof headers === 'object') {
      const targetName = String(name).toLowerCase();
      for (const key of Object.keys(headers)) {
        if (key.toLowerCase() === targetName) {
          return headers[key] ?? null;
        }
      }

      return null;
    }

    return null;
  }

  function toDocsPartialUrl(urlLike) {
    const url = toUrl(urlLike);
    if (!url) {
      return null;
    }

    const path = url.pathname;

    if (!isDocsPath(path)) {
      return null;
    }

    if (path === '/docs') {
      return null;
    }

    if (path.endsWith('.partial.html') || path.endsWith('/search-index.json')) {
      return null;
    }

    if (path.endsWith('/')) {
      url.pathname = `${path}index.partial.html`;
      return url;
    }

    if (path.endsWith('.html')) {
      url.pathname = `${path.slice(0, -5)}.partial.html`;
      return url;
    }

    url.pathname = `${path}.partial.html`;
    return url;
  }

  function toDocsCanonicalUrl(urlLike) {
    const url = toUrl(urlLike);
    if (!url) {
      return null;
    }

    const path = url.pathname;
    if (!isDocsPath(path) || !path.endsWith('.partial.html')) {
      return url;
    }

    let canonicalPath;
    if (path.endsWith('/index.partial.html')) {
      canonicalPath = path.slice(0, -'/index.partial.html'.length);
    } else {
      canonicalPath = path.slice(0, -'.partial.html'.length);
      if (canonicalPath.endsWith('/index')) {
        canonicalPath = canonicalPath.slice(0, -'/index'.length);
      }
    }

    url.pathname = canonicalPath || '/docs';
    return url;
  }

  function replaceBrowserUrl(urlLike) {
    const nextUrl = toUrl(urlLike);
    if (!nextUrl || nextUrl.origin !== window.location.origin) {
      return;
    }

    const currentUrl = new URL(window.location.href);
    if (
      currentUrl.pathname === nextUrl.pathname
      && currentUrl.search === nextUrl.search
      && currentUrl.hash === nextUrl.hash
    ) {
      return;
    }

    window.history.replaceState(
      window.history.state,
      '',
      `${nextUrl.pathname}${nextUrl.search}${nextUrl.hash}`
    );
  }

  function installDocsPartialHook() {
    if (!enableDocsPartialRewrite) {
      return;
    }

    document.addEventListener('turbo:before-fetch-request', (event) => {
      const targetFrame = event.target;
      const turboFrameHeader = getHeader(event.detail?.fetchOptions?.headers, 'Turbo-Frame');
      const isDocFrameRequest = (targetFrame && targetFrame.id === docsFrameId) || turboFrameHeader === docsFrameId;
      if (!isDocFrameRequest) {
        return;
      }

      const requestUrl = event.detail?.url;
      const canonicalUrl = toUrl(requestUrl);
      const partialUrl = toDocsPartialUrl(canonicalUrl);
      if (!partialUrl) {
        return;
      }

      if (!(requestUrl instanceof URL)) {
        return;
      }

      const docsFrame = (targetFrame && targetFrame.id === docsFrameId)
        ? targetFrame
        : document.getElementById(docsFrameId);
      if (docsFrame && canonicalUrl) {
        docsFrame.setAttribute('data-rw-canonical-url', canonicalUrl.toString());
      }

      requestUrl.pathname = partialUrl.pathname;
      requestUrl.search = partialUrl.search;
      requestUrl.hash = partialUrl.hash;
    });

    document.addEventListener('turbo:frame-load', (event) => {
      const frame = event.target;
      if (!frame || frame.id !== docsFrameId) {
        return;
      }

      const pendingCanonical = frame.getAttribute('data-rw-canonical-url');
      if (pendingCanonical) {
        frame.removeAttribute('data-rw-canonical-url');
        replaceBrowserUrl(pendingCanonical);
        return;
      }

      const canonicalUrl = toDocsCanonicalUrl(window.location.href);
      if (canonicalUrl) {
        replaceBrowserUrl(canonicalUrl);
      }
    });
  }

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

  function normalizeQuery(value) {
    return String(value ?? '').trim().slice(0, maxQueryLength);
  }

  function formatQueryForStatus(value) {
    // biome-ignore lint/suspicious/noControlCharactersInRegex: strips non-printable control characters from status text
    return normalizeQuery(value).replace(/[\u0000-\u001f\u007f]/g, '').replace(/\s+/g, ' ');
  }

  async function init() {
    try {
      await ensureSearchIndexLoaded();
      bindSidebar();
      bindSearchPage();
    } catch (err) {
      reportInitError(err);
    }
  }

  function reportInitError(err) {
    console.error(err);
    const message = getErrorMessage(err);
    const sidebar = getSidebarSearchElements();
    const page = getSearchPageElements();
    setStatus(sidebar.status, message);
    setStatus(page.status, message);
  }

  async function ensureSearchIndexLoaded() {
    if (searchIndex) {
      return;
    }

    if (!searchIndexLoadPromise) {
      searchIndexLoadPromise = loadIndex().catch((err) => {
        searchIndexLoadPromise = null;
        throw err;
      });
    }

    await searchIndexLoadPromise;
  }

  function getErrorMessage(err) {
    const message = String(err?.message ?? '');
    if (message.includes('timed out')) {
      return 'Search index request timed out. Please retry.';
    }

    if (message.includes('MiniSearch runtime is not available')) {
      return 'Search is unavailable: runtime failed to load.';
    }

    if (message.includes('Failed to load search index')) {
      return 'Search index could not be loaded. Please retry.';
    }

    return 'Search is temporarily unavailable.';
  }

  async function loadIndex() {
    if (!window.MiniSearch) {
      throw new Error('MiniSearch runtime is not available.');
    }

    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), fetchTimeoutMs);
    let response;

    try {
      response = await fetch(indexUrl, { credentials: 'same-origin', signal: controller.signal });
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        throw new Error('Search index request timed out.');
      }

      throw err;
    } finally {
      window.clearTimeout(timeout);
    }

    if (!response.ok) {
      throw new Error(`Failed to load search index: ${response.status}`);
    }

    const payload = await response.json();
    const docs = Array.isArray(payload.documents) ? payload.documents : [];

    const MiniSearch = window.MiniSearch;
    searchIndex = new MiniSearch({
      fields: ['title', 'headings', 'bodyText'],
      storeFields: ['id', 'path', 'title', 'snippet'],
      searchOptions: defaultSearchOptions
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

    return searchIndex.search(q.trim(), defaultSearchOptions).slice(0, max);
  }

  function bindSidebar() {
    const sidebar = getSidebarSearchElements();
    const { input, results } = sidebar;
    if (!input || !results) {
      return;
    }

    if (input.getAttribute(sidebarBoundAttribute) === '1') {
      return;
    }

    input.setAttribute(sidebarBoundAttribute, '1');

    let activeIndex = -1;
    let lastRenderedQuery = '';

    const runSearch = debounce(() => {
      const q = normalizeQuery(input.value);
      const queryResults = query(q, topResults);
      activeIndex = queryResults.length > 0 ? 0 : -1;
      renderSidebarResults(sidebar, queryResults, q, activeIndex);
      lastRenderedQuery = q;
    }, 120);

    input.addEventListener('input', () => {
      activeIndex = -1;
      runSearch();
    });

    input.addEventListener('keydown', (event) => {
      const currentQuery = normalizeQuery(input.value);
      if (currentQuery !== lastRenderedQuery) {
        const refreshed = query(currentQuery, topResults);
        activeIndex = refreshed.length > 0 ? 0 : -1;
        renderSidebarResults(sidebar, refreshed, currentQuery, activeIndex);
        lastRenderedQuery = currentQuery;
      }

      const items = Array.from(results.querySelectorAll('[role="option"]'));
      if (!items.length) {
        return;
      }

      if (event.key === 'ArrowDown') {
        event.preventDefault();
        activeIndex = Math.min(activeIndex + 1, items.length - 1);
        setActiveSidebarOption(items, activeIndex, input);
      } else if (event.key === 'ArrowUp') {
        event.preventDefault();
        activeIndex = Math.max(activeIndex - 1, 0);
        setActiveSidebarOption(items, activeIndex, input);
      } else if (event.key === 'Enter') {
        if (activeIndex >= 0 && items[activeIndex]) {
          event.preventDefault();
          const anchor = items[activeIndex].querySelector('a');
          if (anchor && typeof anchor.click === 'function') {
            anchor.click();
          } else {
            const href = items[activeIndex].getAttribute('data-href');
            if (href) {
              window.location.assign(href);
            }
          }
        }
      } else if (event.key === 'Escape') {
        results.innerHTML = '';
        results.classList.add('hidden');
        input.removeAttribute('aria-activedescendant');
      }
    });
  }

  function bindSearchPage() {
    const page = getSearchPageElements();
    const { root, input, results } = page;
    if (!root || !input || !results) {
      return;
    }

    if (root.getAttribute(searchPageBoundAttribute) === '1') {
      return;
    }

    root.setAttribute(searchPageBoundAttribute, '1');

    const params = new URLSearchParams(window.location.search);
    const initialQuery = normalizeQuery(params.get('q'));
    input.value = initialQuery;
    renderSearchPageResults(page, initialQuery);

    const onInput = debounce(() => {
      const q = normalizeQuery(input.value);
      if (q !== input.value) {
        input.value = q;
      }

      const url = new URL(window.location.href);
      if (q) {
        url.searchParams.set('q', q);
      } else {
        url.searchParams.delete('q');
      }

      window.history.replaceState({}, '', url);
      renderSearchPageResults(page, q);
    }, 120);

    input.addEventListener('input', onInput);
  }

  function renderSidebarResults(sidebar, queryResults, q, activeIndex = -1) {
    const { input, results, status } = sidebar;
    if (!results) {
      return;
    }

    if (!q || !q.trim()) {
      results.innerHTML = '';
      results.classList.add('hidden');
      input?.removeAttribute('aria-activedescendant');
      setStatus(status, '');
      return;
    }

    if (!queryResults.length) {
      results.classList.remove('hidden');
      results.innerHTML = '<li class="docs-search-empty" role="presentation">No matching docs found.</li>';
      input?.removeAttribute('aria-activedescendant');
      setStatus(status, 'No matching docs found.');
      return;
    }

    results.classList.remove('hidden');
    results.innerHTML = queryResults.map((item, index) => {
      const selected = index === activeIndex ? 'true' : 'false';
      return `<li id="docs-search-option-${index}" role="option" aria-selected="${selected}" tabindex="-1" class="docs-search-option" data-href="${escapeHtml(item.path)}">
        <a href="${escapeHtml(item.path)}" data-turbo-frame="doc-content" data-turbo-action="advance">
          <span class="docs-search-option-title">${escapeHtml(item.title)}</span>
          <span class="docs-search-option-path">${escapeHtml(item.path)}</span>
        </a>
      </li>`;
    }).join('');

    setStatus(status, `${queryResults.length} result(s).`);
  }

  function setActiveSidebarOption(items, activeIndex, input) {
    items.forEach((item, i) => {
      const selected = i === activeIndex;
      item.setAttribute('aria-selected', selected ? 'true' : 'false');
      item.classList.toggle('active', selected);
    });

    if (activeIndex >= 0 && items[activeIndex] && input) {
      input.setAttribute('aria-activedescendant', items[activeIndex].id);
      items[activeIndex].scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
    } else {
      input?.removeAttribute('aria-activedescendant');
    }
  }

  function renderSearchPageResults(page, q) {
    const { results, status } = page;
    if (!results) {
      return;
    }

    if (!q || !q.trim()) {
      setStatus(status, 'Type to search across documentation.');
      results.innerHTML = '';
      return;
    }

    const queryResults = query(q, 100);
    const safeQuery = formatQueryForStatus(q);
    setStatus(status, `${queryResults.length} result(s) for "${safeQuery}".`);

    if (!queryResults.length) {
      results.innerHTML = '<p class="docs-search-empty">No results found.</p>';
      return;
    }

    results.innerHTML = queryResults.map((item) => `
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

  function initOnTurboFrameLoad(event) {
    const frame = event.target;
    if (!(frame instanceof Element) || frame.id !== docsFrameId) {
      return;
    }

    init().catch((err) => console.error('Search init failed:', err));
  }

  function runInit() {
    init().catch((err) => console.error('Search init failed:', err));
  }

  function getCurrentUrlKey() {
    return `${window.location.pathname}${window.location.search}${window.location.hash}`;
  }

  installDocsPartialHook();
  const hasTurbo = typeof window !== 'undefined'
    && (Object.prototype.hasOwnProperty.call(window, 'Turbo')
      || typeof window.Turbo !== 'undefined'
      || document.documentElement.hasAttribute('data-turbo'));
  let lastInitializedUrlKey = null;

  function runInitForCurrentUrl() {
    runInit();
    lastInitializedUrlKey = getCurrentUrlKey();
  }

  if (hasTurbo) {
    document.addEventListener('turbo:load', () => {
      const currentUrlKey = getCurrentUrlKey();
      if (currentUrlKey === lastInitializedUrlKey) {
        return;
      }

      runInitForCurrentUrl();
    });
    document.addEventListener('turbo:frame-load', initOnTurboFrameLoad);

    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', runInitForCurrentUrl, { once: true });
    } else {
      runInitForCurrentUrl();
    }
  } else if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', runInit, { once: true });
  } else {
    runInit();
  }
})();
