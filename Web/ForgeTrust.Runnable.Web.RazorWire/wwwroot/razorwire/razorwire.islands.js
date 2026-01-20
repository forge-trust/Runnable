/**
 * RazorWire Islands Hydrator
 */
(function () {
    if (window.RazorWireIslandsInitialized) return;
    window.RazorWireIslandsInitialized = true;

    const initializedElements = new WeakSet();
    const scheduledElements = new WeakSet();

    /**
     * Hydrates all unhydrated RazorWire islands in the document according to each element's data-rw-strategy.
     *
     * For each element with `data-rw-module` that is not yet hydrated, attempts to parse JSON props from
     * `data-rw-props` (falls back to `{}` and logs an error on parse failure) and mounts the island according to
     * `data-rw-strategy`:
     * - `load`: mount immediately
     * - `visible`: mount when the element becomes visible
     * - `idle`: schedule mount during browser idle time (200ms fallback)
     * - `only`: clear the element's content, then mount immediately
     *
     * Successfully mounted elements are marked as hydrated and tracked to avoid duplicate initialization.
     */
    async function hydrateIslands() {
        const islands = document.querySelectorAll('[data-rw-module]:not([data-rw-hydrated])');

        for (const island of islands) {
            // Guard against double-scheduling: check both already-mounted and currently-scheduled elements
            if (initializedElements.has(island) || scheduledElements.has(island)) continue;

            const modulePath = island.getAttribute('data-rw-module');
            if (!modulePath) continue; // Safety guard

            scheduledElements.add(island);
            const strategy = island.getAttribute('data-rw-strategy') || 'load';
            let props = {};
            try {
                props = JSON.parse(island.getAttribute('data-rw-props') || '{}');
            } catch (e) {
                console.error('Failed to parse island props:', e);
            }

            // Fire-and-forget: Do not await mountIslandSafe here.
            // We want all islands to start hydrating in parallel so that a slow network request
            // for one module doesn't block the initialization of subsequent islands on the page.

            if (strategy === 'load') {
                mountIslandSafe(island, modulePath, props);
            } else if (strategy === 'visible') {
                setupIntersectionObserver(island, modulePath, props);
            } else if (strategy === 'idle') {
                if ('requestIdleCallback' in window) {
                    window.requestIdleCallback(() => mountIslandSafe(island, modulePath, props));
                } else {
                    setTimeout(() => mountIslandSafe(island, modulePath, props), 200);
                }
            } else if (strategy === 'only') {
                island.innerHTML = '';
                mountIslandSafe(island, modulePath, props);
            } else {
                // Unknown strategy, cleanup
                console.warn(`Unknown island strategy: ${strategy}`, island);
                scheduledElements.delete(island);
            }
        }
    }

    async function mountIslandSafe(island, modulePath, props) {
        try {
            await mountIsland(island, modulePath, props);
        } finally {
            scheduledElements.delete(island);
        }
    }

    /**
     * Dynamically imports the module at modulePath and, if it exports a `mount` function, calls it to hydrate the provided root element.
     *
     * After a successful mount, marks the root with `data-rw-hydrated="true"` and adds it to the internal initializedElements set.
     * @param {HTMLElement} root - The DOM element to hydrate.
     * @param {string} modulePath - The module specifier used for dynamic import.
     * @param {Record<string, any>} props - Props passed to the module's `mount` function.
     */
    async function mountIsland(root, modulePath, props) {
        try {
            const module = await import(modulePath);
            if (module.mount) {
                module.mount(root, props);
                root.setAttribute('data-rw-hydrated', 'true');
                initializedElements.add(root);
            }
        } catch (e) {
            console.error(`Failed to mount island: ${modulePath}`, e);
        }
    }

    /**
     * Observe an island element and mount its module when the element becomes visible in the viewport.
     *
     * When the island intersects the viewport, the observer stops observing that element and invokes
     * mountIsland(island, modulePath, props) to perform the mount.
     *
     * @param {Element} island - The DOM element representing the island to observe and mount.
     * @param {string} modulePath - The module path to dynamically import for mounting.
     * @param {Object} props - The props object to pass to the module's mount function.
     */
    function setupIntersectionObserver(island, modulePath, props) {
        const observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    observer.unobserve(island);
                    // Use safe mount to ensure cleanup
                    mountIslandSafe(island, modulePath, props);
                }
            }
        });
        observer.observe(island);
    }

    // Listen for Turbo events
    document.addEventListener('turbo:load', hydrateIslands);
    document.addEventListener('turbo:frame-load', hydrateIslands);

    // Initial check
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', hydrateIslands);
    } else {
        hydrateIslands();
    }
})();