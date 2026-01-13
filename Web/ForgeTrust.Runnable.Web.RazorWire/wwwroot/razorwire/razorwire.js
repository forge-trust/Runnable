/**
 * RazorWire Core Client Runtime
 * Provides native stream monitoring and event dispatching.
 */
(function () {
    if (window.RazorWireInitialized) return;
    window.RazorWireInitialized = true;

    class StreamMonitor {
        constructor() {
            this.monitors = new WeakMap(); // Element -> Id
            this.channelStates = new Map(); // Channel -> State (persists across navs)
            this.observer = new MutationObserver(this.handleMutations.bind(this));
        }

        start() {
            this.observeBody();
            this.scan();

            // Re-apply state and re-observe on Turbo navigations
            document.addEventListener('turbo:render', () => {
                const isPreview = document.documentElement.hasAttribute('data-turbo-preview');
                this.observeBody();
                this.restoreStates();
                this.syncDependentElements(); // Sync buttons/elements on render

                // Only scan for and connect to streams on final renders.
                // Previews show stale state, so we just restore the connection badge (above)
                // and wait for the real body to avoid double-connection logs/overhead.
                if (!isPreview) {
                    this.scan();
                }
            });
            document.addEventListener('turbo:load', () => {
                this.syncIslands();
                this.syncDependentElements(); // Sync buttons/elements on load
            });
            document.addEventListener('turbo:frame-load', () => {
                this.scan();
                this.syncDependentElements(); // Sync buttons/elements on frame load
            });
        }

        syncIslands() {
            // Find all permanent islands marked for Stale-While-Revalidate
            const islands = document.querySelectorAll('turbo-frame[data-turbo-permanent][src][data-rw-swr]');
            islands.forEach(frame => {
                // Turbo's .reload() on an existing frame performs a background fetch 
                // and swaps content only when ready, without showing the skeleton.
                if (typeof frame.reload === 'function') {
                    frame.reload();
                }
            });
        }

        observeBody() {
            this.observer.disconnect(); // Prevent double observation
            this.observer.observe(document.body, {
                childList: true,
                subtree: true
            });
        }

        restoreStates() {
            // Re-apply all persisted channel states to the new body
            for (const [channel, state] of this.channelStates) {
                this.updateBodyAttribute(channel, state);
            }
        }

        handleMutations(mutations) {
            for (const mutation of mutations) {
                for (const node of mutation.addedNodes) {
                    if (node instanceof Element) {
                        if (node.tagName === 'TURBO-STREAM-SOURCE') {
                            this.monitor(node);
                        } else {
                            // Deep scan added subtrees
                            node.querySelectorAll('turbo-stream-source').forEach(el => this.monitor(el));
                        }
                    }
                }
                for (const node of mutation.removedNodes) {
                    if (node instanceof Element) {
                        if (node.tagName === 'TURBO-STREAM-SOURCE') {
                            this.unmonitor(node);
                        } else {
                            node.querySelectorAll('turbo-stream-source').forEach(el => this.unmonitor(el));
                        }
                    }
                }
            }
        }

        scan() {
            document.querySelectorAll('turbo-stream-source').forEach(el => this.monitor(el));
        }

        monitor(element) {
            if (this.monitors.has(element)) return;

            const channel = this.getChannelName(element.getAttribute('src'));
            if (!channel) return;

            // 1. Initialize entry immediately
            const entry = { interval: null, currentState: null };
            this.monitors.set(element, entry);

            // 2. Define the check function
            const checkState = () => {
                let readyState = 0; // Default to connecting

                if (element.streamSource) {
                    readyState = element.streamSource.readyState;
                } else if (element.stream && element.stream.eventSource) {
                    readyState = element.stream.eventSource.readyState;
                }

                let newState = 'connecting';
                if (readyState === 1) newState = 'connected';
                else if (readyState === 2) newState = 'disconnected';

                // Check if detached
                if (!document.body.contains(element)) {
                    this.unmonitor(element);
                    return;
                }

                this.updateState(element, channel, newState);
            };

            // 3. Run check immediately
            checkState();

            // 4. Start Interval
            entry.interval = setInterval(checkState, 500);
        }

        unmonitor(element) {
            const data = this.monitors.get(element);
            if (data) {
                clearInterval(data.interval);
                this.monitors.delete(element);

                const channel = this.getChannelName(element.getAttribute('src'));
                if (channel) {
                    // Set disconnected state
                    this.updateStateGeneric(channel, 'disconnected');

                    // If we were active, fire detached/disconnected event
                    if (data.currentState === 'connected' || data.currentState === 'connecting') {
                        // Global event for cleanup/logging
                        const event = new CustomEvent('razorwire:stream:disconnected', {
                            bubbles: true,
                            cancelable: false,
                            detail: {
                                channel: channel,
                                source: element,
                                state: 'disconnected'
                            }
                        });
                        document.dispatchEvent(event);
                    }
                }
            }
        }

        getChannelName(src) {
            if (!src) return null;
            try {
                const url = new URL(src, window.location.origin);
                const segments = url.pathname.split('/');
                return segments.pop() || segments.pop();
            } catch {
                return null;
            }
        }

        updateState(element, channel, state) {
            const data = this.monitors.get(element);
            if (!data) return; // Should not happen if monitored

            if (data.currentState !== state) {
                data.currentState = state;

                // Update global state and body attribute
                this.updateStateGeneric(channel, state);

                // Dispatch Event
                // We dispatch only to the element and let it bubble to document.
                const eventName = `razorwire:stream:${state}`;
                const event = new CustomEvent(eventName, {
                    bubbles: true,
                    cancelable: false,
                    detail: {
                        channel: channel,
                        source: element,
                        state: state
                    }
                });
                element.dispatchEvent(event); // Bubbles to document
            }
        }

        // Generic state updater that doesn't require a DOM element reference
        // Used for unmonitor and restoring state
        updateStateGeneric(channel, state) {
            this.channelStates.set(channel, state);
            this.updateBodyAttribute(channel, state);
            this.syncDependentElements(channel);
        }

        syncDependentElements(targetChannel = null) {
            const selector = targetChannel
                ? `[data-rw-requires-stream="${targetChannel}"]`
                : '[data-rw-requires-stream]';

            const elements = document.querySelectorAll(selector);
            elements.forEach(el => {
                const channel = el.getAttribute('data-rw-requires-stream');
                const state = this.channelStates.get(channel);

                if (state === 'connected') {
                    el.removeAttribute('disabled');
                } else {
                    el.setAttribute('disabled', 'disabled');
                }
            });
        }

        updateBodyAttribute(channel, state) {
            const attr = `data-rw-stream-${channel}`;
            if (state) {
                document.body.setAttribute(attr, state);
            } else {
                document.body.removeAttribute(attr);
            }
        }
    }

    // Initialize
    const monitor = new StreamMonitor();
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => monitor.start());
    } else {
        monitor.start();
    }

    window.RazorWire = { monitor };
    console.log('✅ RazorWire Runtime Initialized');
})();
