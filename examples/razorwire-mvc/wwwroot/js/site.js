// RazorWire Site initialization
(function () {
    if (window.RazorWireDiagnosticsInitialized) return;
    window.RazorWireDiagnosticsInitialized = true;

    // Log Turbo Drive events for debugging
    document.addEventListener('turbo:load', () => console.log('🚀 Turbo Drive: Page Loaded'));
    document.addEventListener('turbo:render', () => console.log('🎨 Turbo Drive: Body Rendered'));
    document.addEventListener('turbo:visit', (e) => console.log('🚗 Turbo Drive: Visiting', e.detail.url));

    // Handle autofocus manually to avoid browser warnings during Turbo swaps
    document.addEventListener('turbo:load', () => {
        const autofocusElement = document.querySelector('[data-autofocus]');
        if (autofocusElement && document.activeElement === document.body) {
            autofocusElement.focus();
        }
    });

    console.log('✅ RazorWire Diagnostics Initialized');
})();
