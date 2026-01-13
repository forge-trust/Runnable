// RazorWire Site initialization
(function () {
    if (window.RazorWireDiagnosticsInitialized) return;
    window.RazorWireDiagnosticsInitialized = true;

    // Log Turbo Drive events for debugging
    document.addEventListener('turbo:load', () => console.log('🚀 Turbo Drive: Page Loaded'));
    document.addEventListener('turbo:render', () => console.log('🎨 Turbo Drive: Body Rendered'));
    document.addEventListener('turbo:visit', (e) => console.log('🚗 Turbo Drive: Visiting', e.detail.url));

    // Monitor SSE Connection Status
    // Monitor SSE Connection Status
    let connectionInterval;

    function updateConnectionStatus() {
        // Clear existing interval to prevent duplicates
        if (connectionInterval) clearInterval(connectionInterval);

        const source = document.querySelector('turbo-stream-source');
        const badge = document.getElementById('connection-badge');

        // If we are not on the reactivity page, stop monitoring
        if (!source || !badge) return;

        const setConnecting = () => {
            badge.innerHTML = `
                <span class="flex h-2 w-2 rounded-full bg-amber-500 animate-pulse"></span>
                <span class="text-xs font-medium text-amber-600 uppercase">Connecting...</span>
             `;
        };

        const setConnected = () => {
            badge.innerHTML = `
                <span class="flex h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span>
                <span class="text-xs font-medium text-emerald-600 uppercase">Connected</span>
             `;
        };

        const setDisconnected = () => {
            badge.innerHTML = `
                <span class="flex h-2 w-2 rounded-full bg-rose-500"></span>
                <span class="text-xs font-medium text-rose-600 uppercase">Disconnected</span>
             `;
        };

        // Poll logic for robust state tracking
        // readyState: 0=CONNECTING, 1=OPEN, 2=CLOSED
        const checkState = () => {
            if (source.streamSource) {
                const state = source.streamSource.readyState;
                if (state === 1) setConnected();
                else if (state === 0) setConnecting();
                else setDisconnected();
            } else {
                setConnecting();
            }
        };

        // Check immediately and then poll
        checkState();
        connectionInterval = setInterval(checkState, 500);
    }

    document.addEventListener('turbo:load', updateConnectionStatus);
    document.addEventListener('turbo:render', updateConnectionStatus);

    // Handle autofocus manually to avoid browser warnings during Turbo swaps
    document.addEventListener('turbo:load', () => {
        const autofocusElement = document.querySelector('[data-autofocus]');
        if (autofocusElement && document.activeElement === document.body) {
            autofocusElement.focus();
        }
    });

    if (!document.startViewTransition) {
        console.warn('⚠️ View Transition API not supported in this browser. Morphing animations will be skipped.');
    } else {
        console.log('✨ View Transition API Supported');
    }

    console.log('✅ RazorWire Diagnostics Initialized');
})();
