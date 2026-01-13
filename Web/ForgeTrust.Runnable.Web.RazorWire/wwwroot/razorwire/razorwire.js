/**
 * RazorWire Core Client Runtime
 * Provides native stream monitoring and event dispatching.
 */
(function () {
    if (window.RazorWireInitialized) return;
    window.RazorWireInitialized = true;

    class StreamMonitor {
        constructor() {
            this.monitors = new WeakMap(); // Element -> IntervalId
            this.observer = new MutationObserver(this.handleMutations.bind(this));
        }

        start() {
            this.observer.observe(document.body, {
                childList: true,
                subtree: true
            });
            this.scan();
            document.addEventListener('turbo:load', () => this.scan());
            document.addEventListener('turbo:frame-load', () => this.scan());
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

            // 1. Initialize entry immediately to prevent race conditions or recursion
            // We store the interval ID later
            const entry = { interval: null, currentState: null };
            this.monitors.set(element, entry);

            // 2. Define the check function (polled and immediate)
            const checkState = () => {
                // Robust polling of readyState
                // 0=CONNECTING, 1=OPEN, 2=CLOSED
                let readyState = 0; // Default to connecting if unknown

                if (element.streamSource) {
                    readyState = element.streamSource.readyState;
                } else if (element.stream && element.stream.eventSource) {
                    readyState = element.stream.eventSource.readyState;
                }

                let newState = 'connecting';
                if (readyState === 1) newState = 'connected';
                else if (readyState === 2) newState = 'disconnected';

                // Check if detached (safety guard for intervals)
                if (!document.body.contains(element)) {
                    this.unmonitor(element);
                    return;
                }

                this.updateState(element, channel, newState);
            };

            // 3. Run check immediately (sync)
            checkState();

            // 4. Start Interval and update entry
            entry.interval = setInterval(checkState, 500);
        }

        unmonitor(element) {
            const data = this.monitors.get(element);
            if (data) {
                clearInterval(data.interval);
                this.monitors.delete(element);

                const channel = this.getChannelName(element.getAttribute('src'));
                if (channel) {
                    // If we were connected, fire a final disconnected event/state update
                    if (data.currentState === 'connected' || data.currentState === 'connecting') {
                        this.updateBodyAttribute(channel, null); // Clear attribute first

                        // Dispatch disconnected event to notify listeners
                        const event = new CustomEvent('razorwire:stream:disconnected', {
                            bubbles: true,
                            cancelable: false,
                            detail: {
                                channel: channel,
                                source: element,
                                state: 'disconnected'
                            }
                        });
                        element.dispatchEvent(event);
                    } else {
                        this.updateBodyAttribute(channel, null);
                    }
                }
            }
        }

        getChannelName(src) {
            if (!src) return null;
            try {
                // Extract last segment of URL as channel name
                // e.g. /_rw/streams/reactivity -> reactivity
                const url = new URL(src, window.location.origin);
                const segments = url.pathname.split('/');
                return segments.pop() || segments.pop(); // Handle trailing slash
            } catch {
                return null;
            }
        }

        updateState(element, channel, state) {
            const data = this.monitors.get(element);
            if (!data) return;

            if (data.currentState !== state) {
                data.currentState = state;

                // 1. Update Body Attribute
                this.updateBodyAttribute(channel, state);

                // 2. Dispatch Event
                // We dispatch only to the element and let it bubble to document.
                // This prevents duplicate events (one direct, one bubbled).
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
