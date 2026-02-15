(() => {
  const indexUrl = '/docs/search-index.json';
  const maxQueryLength = 500;
  const topResults = 8;
  const fetchTimeoutMs = 10000;
  const defaultSearchOptions = {
    prefix: true,
    fuzzy: 0.1,
    boost: { title: 6, headings: 3, bodyText: 1 }
  };
  let searchIndex = null;

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

  function normalizeQuery(value) {
    return String(value ?? '').trim().slice(0, maxQueryLength);
  }

  function formatQueryForStatus(value) {
    // biome-ignore lint/suspicious/noControlCharactersInRegex: strips non-printable control characters from status text
    return normalizeQuery(value).replace(/[\u0000-\u001f\u007f]/g, '').replace(/\s+/g, ' ');
  }

  async function init() {
    try {
      await loadIndex();
      bindSidebar();
      bindSearchPage();
    } catch (err) {
      console.error(err);
      const message = getErrorMessage(err);
      setStatus(sidebarStatus, message);
      setStatus(pageStatus, message);
    }
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
      response = await fetch(indexUrl, { credentials: 'omit', signal: controller.signal });
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
    if (!sidebarInput || !sidebarResults) {
      return;
    }

    let activeIndex = -1;
    let lastRenderedQuery = '';

    const runSearch = debounce(() => {
      const q = normalizeQuery(sidebarInput.value);
      const results = query(q, topResults);
      activeIndex = results.length > 0 ? 0 : -1;
      renderSidebarResults(results, q, activeIndex);
      lastRenderedQuery = q;
    }, 120);

    sidebarInput.addEventListener('input', () => {
      activeIndex = -1;
      runSearch();
    });

    sidebarInput.addEventListener('keydown', (event) => {
      const currentQuery = normalizeQuery(sidebarInput.value);
      if (currentQuery !== lastRenderedQuery) {
        const refreshed = query(currentQuery, topResults);
        activeIndex = refreshed.length > 0 ? 0 : -1;
        renderSidebarResults(refreshed, currentQuery, activeIndex);
        lastRenderedQuery = currentQuery;
      }

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
        sidebarInput.removeAttribute('aria-activedescendant');
      }
    });
  }

  function bindSearchPage() {
    if (!pageRoot || !pageInput || !pageResults) {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const initialQuery = normalizeQuery(params.get('q'));
    pageInput.value = initialQuery;
    renderSearchPageResults(initialQuery);

    const onInput = debounce(() => {
      const q = normalizeQuery(pageInput.value);
      if (q !== pageInput.value) {
        pageInput.value = q;
      }

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

  function renderSidebarResults(results, q, activeIndex = -1) {
    if (!sidebarResults) {
      return;
    }

    if (!q || !q.trim()) {
      sidebarResults.innerHTML = '';
      sidebarResults.classList.add('hidden');
      sidebarInput?.removeAttribute('aria-activedescendant');
      setStatus(sidebarStatus, '');
      return;
    }

    if (!results.length) {
      sidebarResults.classList.remove('hidden');
      sidebarResults.innerHTML = '<li class="docs-search-empty" role="option">No matching docs found.</li>';
      sidebarInput?.removeAttribute('aria-activedescendant');
      setStatus(sidebarStatus, 'No matching docs found.');
      return;
    }

    sidebarResults.classList.remove('hidden');
    sidebarResults.innerHTML = results.map((item, index) => {
      const selected = index === activeIndex ? 'true' : 'false';
      return `<li id="docs-search-option-${index}" role="option" aria-selected="${selected}" tabindex="-1" class="docs-search-option" data-href="${escapeHtml(item.path)}">
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

    if (activeIndex >= 0 && items[activeIndex] && sidebarInput) {
      sidebarInput.setAttribute('aria-activedescendant', items[activeIndex].id);
    } else {
      sidebarInput?.removeAttribute('aria-activedescendant');
    }
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
    const safeQuery = formatQueryForStatus(q);
    setStatus(pageStatus, `${results.length} result(s) for "${safeQuery}".`);

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
