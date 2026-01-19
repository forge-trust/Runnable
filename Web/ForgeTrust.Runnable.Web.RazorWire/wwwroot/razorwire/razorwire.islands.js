/**
 * RazorWire Islands Hydrator
 */
(function () {
    if (window.RazorWireIslandsInitialized) return;
    window.RazorWireIslandsInitialized = true;

    const initializedElements = new WeakSet();

    /**
     * Finds unhydrated island elements (marked with `data-rw-module`) and mounts each according to its `data-rw-strategy`.
     *
     * Iterates DOM elements that have `data-rw-module` and are not yet hydrated, reads optional JSON props from
     * `data-rw-props` (falls back to `{}` and logs an error on parse failure), and mounts each island according to
     * its `data-rw-strategy`: `load` mounts immediately, `visible` mounts when the element becomes visible, `idle`
     * schedules mounting during browser idle time (with a 200ms fallback), and `only` clears the element before mounting.
     * Hydration state is marked on the element and tracked to avoid duplicate initialization.
     */
    async function hydrateIslands() {
        const islands = document.querySelectorAll('[data-rw-module]:not([data-rw-hydrated])');

        for (const island of islands) {
            if (initializedElements.has(island)) continue;

            const modulePath = island.getAttribute('data-rw-module');
            const strategy = island.getAttribute('data-rw-strategy') || 'load';
            let props = {};
            try {
                props = JSON.parse(island.getAttribute('data-rw-props') || '{}');
            } catch (e) {
                console.error('Failed to parse island props:', e);
            }

            if (strategy === 'load') {
                await mountIsland(island, modulePath, props);
            } else if (strategy === 'visible') {
                setupIntersectionObserver(island, modulePath, props);
            } else if (strategy === 'idle') {
                if ('requestIdleCallback' in window) {
                    window.requestIdleCallback(() => mountIsland(island, modulePath, props));
                } else {
                    setTimeout(() => mountIsland(island, modulePath, props), 200);
                }
            } else if (strategy === 'only') {
                island.innerHTML = '';
                await mountIsland(island, modulePath, props);
            }
        }
    }

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

    function setupIntersectionObserver(island, modulePath, props) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(async (entry) => {
                if (entry.isIntersecting) {
                    observer.unobserve(island);
                    await mountIsland(island, modulePath, props);
                }
            });
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