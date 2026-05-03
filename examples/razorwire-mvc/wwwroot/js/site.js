// RazorWire Site initialization
(function () {
    if (window.RazorWireDiagnosticsInitialized) return;
    window.RazorWireDiagnosticsInitialized = true;

    // Log Turbo Drive events for debugging
    document.addEventListener('turbo:load', () => console.log('🚀 Turbo Drive: Page Loaded'));
    document.addEventListener('turbo:render', () => console.log('🎨 Turbo Drive: Body Rendered'));
    document.addEventListener('turbo:visit', (e) => console.log('🚗 Turbo Drive: Visiting', e.detail.url));

    // Log RazorWire Stream events
    document.addEventListener('razorwire:stream:connecting', (e) => console.log(`⏳ Stream [${e.detail?.channel}${e.detail?.source?.id ? `:#${e.detail.source.id}` : ''}] Connecting...`));
    document.addEventListener('razorwire:stream:connected', (e) => console.log(`🟢 Stream [${e.detail?.channel}${e.detail?.source?.id ? `:#${e.detail.source.id}` : ''}] Connected`));
    document.addEventListener('razorwire:stream:disconnected', (e) => console.log(`🔴 Stream [${e.detail?.channel}${e.detail?.source?.id ? `:#${e.detail.source.id}` : ''}] Disconnected`));

    // Handle autofocus manually to avoid browser warnings during Turbo swaps
    document.addEventListener('turbo:load', () => {
        const autofocusElement = document.querySelector('[data-autofocus]');
        if (autofocusElement && document.activeElement === document.body) {
            autofocusElement.focus();
        }
    });

    document.addEventListener('razorwire:form:failure', (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;

        const manualTargetSelector = form.getAttribute('data-demo-manual-target');
        if (!manualTargetSelector) return;

        const target = document.querySelector(manualTargetSelector);
        if (!target) return;

        event.preventDefault();
        target.innerHTML = '';
        const block = document.createElement('div');
        block.setAttribute('role', 'alert');
        block.setAttribute('tabindex', '-1');
        block.className = 'rounded-md border border-fuchsia-300 bg-fuchsia-50 px-3 py-2 text-sm text-fuchsia-950';
        block.innerHTML = '<strong class="block font-semibold">Manual handler rendered this.</strong><p class="mt-1"></p>';
        block.querySelector('p').textContent = event.detail?.message || 'The form failed.';
        target.appendChild(block);
        block.focus({ preventScroll: true });
    });

    if (!document.startViewTransition) {
        console.warn('⚠️ View Transition API not supported in this browser. Morphing animations will be skipped.');
    } else {
        console.log('✨ View Transition API Supported');
    }

    console.log('✅ RazorWire Diagnostics Initialized (RazorWireWebExample)');

})();
