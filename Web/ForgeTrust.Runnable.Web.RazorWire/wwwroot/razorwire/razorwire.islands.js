/**
 * RazorWire Islands Hydrator
 */
(function () {
    const initializedElements = new WeakSet();

    async function hydrateIslands() {
        const islands = document.querySelectorAll('[data-rw-module]:not([data-rw-hydrated])');

        for (const island of islands) {
            if (initializedElements.has(island)) continue;

            const modulePath = island.getAttribute('data-rw-module');
            const strategy = island.getAttribute('data-rw-strategy') || 'load';
            const props = JSON.parse(island.getAttribute('data-rw-props') || '{}');

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
