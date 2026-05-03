(() => {
    const outlineSelector = "#docs-page-outline";
    const outlineLinkSelector = "a[data-doc-outline-link='true']";
    const compactMediaQuery = "(max-width: 1279px)";

    let lifecycleController = null;
    let activeObserver = null;

    function decodeHash(hash) {
        if (!hash) {
            return null;
        }

        try {
            return decodeURIComponent(hash.replace(/^#/, ""));
        } catch {
            return null;
        }
    }

    function getLinkTargetId(link) {
        let url;
        try {
            url = new URL(link.href, window.location.origin);
        } catch {
            return null;
        }

        if (!url.hash) {
            return null;
        }

        return decodeHash(url.hash);
    }

    function setExpanded(shell, toggle, expanded) {
        shell.dataset.outlineExpanded = expanded ? "true" : "false";
        toggle?.setAttribute("aria-expanded", expanded ? "true" : "false");
    }

    function setActiveLink(links, link, currentLabel) {
        for (const candidate of links) {
            const isActive = candidate === link;
            candidate.classList.toggle("docs-outline-link--active", isActive);

            if (isActive) {
                candidate.setAttribute("aria-current", "location");
                if (currentLabel) {
                    currentLabel.textContent = candidate.textContent?.trim() ?? "";
                }
            } else {
                candidate.removeAttribute("aria-current");
            }
        }
    }

    function getActiveLinkFromHash(links) {
        const targetId = decodeHash(window.location.hash);
        if (!targetId) {
            return null;
        }

        return links.find(link => getLinkTargetId(link) === targetId) ?? null;
    }

    function refreshHashActiveLink(links, expectedLink, currentLabel) {
        if (getActiveLinkFromHash(links) === expectedLink) {
            setActiveLink(links, expectedLink, currentLabel);
        }
    }

    function getOutlineEntries(links) {
        return links
            .map(link => {
                const targetId = getLinkTargetId(link);
                const target = targetId ? document.getElementById(targetId) : null;
                return target ? { link, target } : null;
            })
            .filter(entry => entry !== null);
    }

    function getActiveEntryFromScrollPosition(entries, root) {
        const rootTop = root.getBoundingClientRect().top;
        const activationTop = rootTop + 64;
        let activeEntry = entries[0];

        for (const entry of entries) {
            if (entry.target.getBoundingClientRect().top > activationTop) {
                break;
            }

            activeEntry = entry;
        }

        return activeEntry;
    }

    function disconnectActiveObserver() {
        activeObserver?.disconnect();
        activeObserver = null;
    }

    function teardown() {
        disconnectActiveObserver();
        lifecycleController?.abort();
        lifecycleController = null;
    }

    function initOutline() {
        teardown();

        const shell = document.querySelector(outlineSelector);
        if (!(shell instanceof HTMLElement)) {
            return;
        }

        const mainContent = document.getElementById("main-content");
        const toggle = shell.querySelector("[data-doc-outline-toggle='true']");
        const currentLabel = shell.querySelector("[data-doc-outline-current]");
        const links = Array.from(shell.querySelectorAll(outlineLinkSelector))
            .filter(link => link instanceof HTMLAnchorElement);

        if (links.length === 0) {
            return;
        }

        const controller = new AbortController();
        lifecycleController = controller;
        shell.dataset.outlineEnhanced = "true";

        const compactMedia = window.matchMedia ? window.matchMedia(compactMediaQuery) : null;
        const syncViewportState = () => {
            setExpanded(shell, toggle, !(compactMedia?.matches ?? false));
        };

        syncViewportState();
        compactMedia?.addEventListener("change", syncViewportState, { signal: controller.signal });

        toggle?.addEventListener("click", () => {
            setExpanded(shell, toggle, shell.dataset.outlineExpanded !== "true");
        }, { signal: controller.signal });

        for (const link of links) {
            link.addEventListener("click", () => {
                setActiveLink(links, link, currentLabel);
                if (compactMedia?.matches) {
                    setExpanded(shell, toggle, false);
                }

                for (const delay of [120, 360, 720]) {
                    window.setTimeout(() => refreshHashActiveLink(links, link, currentLabel), delay);
                }
            }, { signal: controller.signal });
        }

        const initialActiveLink = getActiveLinkFromHash(links) ?? links[0];
        setActiveLink(links, initialActiveLink, currentLabel);

        window.addEventListener("hashchange", () => {
            const hashActiveLink = getActiveLinkFromHash(links);
            if (hashActiveLink) {
                setActiveLink(links, hashActiveLink, currentLabel);
            }
        }, { signal: controller.signal });

        const entries = getOutlineEntries(links);
        if (entries.length === 0 || !("IntersectionObserver" in window) || !mainContent) {
            return;
        }

        activeObserver = new IntersectionObserver(
            observedEntries => {
                if (!observedEntries.some(entry => entry.isIntersecting)) {
                    return;
                }

                const activeEntry = getActiveEntryFromScrollPosition(entries, mainContent);
                if (activeEntry) {
                    setActiveLink(links, activeEntry.link, currentLabel);
                }
            },
            {
                root: mainContent,
                rootMargin: "-18% 0px -68% 0px",
                threshold: [0, 1]
            });

        for (const entry of entries) {
            activeObserver.observe(entry.target);
        }
    }

    document.addEventListener("DOMContentLoaded", initOutline);
    document.addEventListener("turbo:load", initOutline);
    document.addEventListener("turbo:frame-load", event => {
        if (event.target?.id === "doc-content") {
            initOutline();
        }
    });
})();
