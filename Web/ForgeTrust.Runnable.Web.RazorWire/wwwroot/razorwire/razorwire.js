/**
 * RazorWire Core Client Runtime
 * Provides native stream monitoring and event dispatching.
 */
(function () {
    if (window.RazorWireInitialized) return;
    window.RazorWireInitialized = true;

    class ConnectionManager {
        constructor() {
            this.sources = new Map(); // src -> { es: EventSource, elements: Set<Element>, closeTimer: int, state: string }
            this.channelStates = new Map(); // Channel -> State (for global access and dependent elements)
            this.observer = new MutationObserver(this.handleMutations.bind(this));
        }

        start() {
            this.observeBody();
            this.scan();

            document.addEventListener('turbo:render', () => {
                const isPreview = document.documentElement.hasAttribute('data-turbo-preview');
                this.observeBody();

                // restoreStates MUST run to apply 'connected' class to the new body
                this.restoreStates();

                // Scan FIRST to register new elements and keep the connection alive
                if (!isPreview) {
                    this.scan();
                }

                // Prune any elements that were removed (Turbo replaces body, so MO might miss them)
                this.prune();

                this.syncDependentElements();
            });
            document.addEventListener('turbo:load', () => {
                this.syncDependentElements();
                this.syncIslands();
            });
            document.addEventListener('turbo:frame-load', () => {
                this.scan();
                this.syncDependentElements();
            });
        }

        restoreStates() {
            for (const [channel, state] of this.channelStates) {
                this.updateBodyAttribute(channel, state);
            }
        }

        prune() {
            // console.log('[ConnectionManager] Pruning disconnected elements...');
            for (const [src, source] of this.sources) {
                for (const element of source.elements) {
                    if (!element.isConnected) {
                        // console.log('[ConnectionManager] Pruning element:', src);
                        this.unregister(element);
                    }
                }
            }
        }

        syncIslands(targetChannel = null) {
            const selector = targetChannel
                ? `turbo-frame[data-turbo-permanent][src][data-rw-swr][data-rw-requires-stream="${targetChannel}"]`
                : 'turbo-frame[data-turbo-permanent][src][data-rw-swr]';

            const islands = document.querySelectorAll(selector);



            setTimeout(() => {
                islands.forEach(frame => {
                    // Optimized SWR:
                    // If a frame is LAZY and hasn't loaded yet (no 'complete' attribute),
                    // we skip the reload. The native lazy load will happen when visible, providing fresh data.
                    const isLazy = frame.getAttribute('loading') === 'lazy';
                    const hasLoadedOnce = frame.hasAttribute('complete');

                    if (isLazy && !hasLoadedOnce) {
                        return;
                    }

                    if (typeof frame.reload === 'function') {
                        frame.reload();
                    }
                });
            }, 100);
        }

        observeBody() {
            this.observer.disconnect();
            this.observer.observe(document.body, {
                childList: true,
                subtree: true
            });
        }

        handleMutations(mutations) {
            for (const mutation of mutations) {
                for (const node of mutation.addedNodes) {
                    if (node instanceof Element) {
                        if (node.tagName === 'RW-STREAM-SOURCE') {
                            this.register(node);
                        } else {
                            node.querySelectorAll('rw-stream-source').forEach(el => this.register(el));
                        }
                    }
                }
                for (const node of mutation.removedNodes) {
                    if (node instanceof Element) {
                        if (node.tagName === 'RW-STREAM-SOURCE') {
                            this.unregister(node);
                        } else {
                            node.querySelectorAll('rw-stream-source').forEach(el => this.unregister(el));
                        }
                    }
                }
            }
            this.syncDependentElements();
        }

        scan() {
            document.querySelectorAll('rw-stream-source').forEach(el => this.register(el));
        }

        register(element) {
            const src = element.getAttribute('src');
            if (!src) return;

            let source = this.sources.get(src);
            if (!source) {
                console.log('[ConnectionManager] Creating NEW connection for:', src);
                // Create new persistent connection
                const es = new EventSource(src);
                Turbo.connectStreamSource(es);

                source = {
                    es,
                    elements: new Set(),
                    closeTimer: null,
                    state: 'connecting',
                    channel: this.getChannelName(src)
                };

                // Hook up state listeners to the EventSource directly
                es.onopen = () => this.updateSourceState(src, 'connected');
                es.onerror = () => {
                    if (es.readyState === 2) this.updateSourceState(src, 'disconnected');
                    else this.updateSourceState(src, 'connecting');
                };

                this.sources.set(src, source);

                // Initial state
                this.updateSourceState(src, 'connecting');
            }

            // Cancel any pending close
            if (source.closeTimer) {
                console.log('[ConnectionManager] Cancelling close timer for:', src);
                clearTimeout(source.closeTimer);
                source.closeTimer = null;
            }

            // Only increment count if we haven't already registered this specific element
            // (MutationObserver might fire multiple times or scan might duplicate)
            if (!source.elements.has(element)) {
                source.elements.add(element);
                element.setAttribute('data-rw-registered', 'true');
            }

            // Sync this element with current state
            this.dispatchToElement(element, source.channel, source.state);
        }

        unregister(element) {
            const src = element.getAttribute('src');
            if (!src) return;

            const source = this.sources.get(src);
            if (!source) return;

            if (source.elements.has(element)) {
                source.elements.delete(element);
                element.removeAttribute('data-rw-registered');
            }

            if (source.elements.size === 0) {
                console.log('[ConnectionManager] No more elements for:', src, 'Starting grace period (5000ms)...');

                // Capture the last known element info for reporting
                const lastId = element.id || '';

                // Grace period: Wait 5000ms before actually closing.
                // If a new page loads with the same stream, register() will cancel this timer.
                source.closeTimer = setTimeout(() => {
                    // Check size again in case re-registered
                    if (source.elements.size > 0) {
                        console.log('[ConnectionManager] Grace period saved connection:', src);
                        return;
                    }

                    console.log('[ConnectionManager] Closing connection:', src);
                    source.es.close();
                    Turbo.disconnectStreamSource(source.es);
                    this.sources.delete(src);

                    this.updateSourceState(src, 'disconnected');

                    // Fire disconnected event globally since source is gone
                    const event = new CustomEvent('razorwire:stream:disconnected', {
                        bubbles: true,
                        cancelable: false,
                        detail: {
                            channel: source.channel,
                            // Provide a mockup of the source so logging doesn't crash
                            source: { id: lastId },
                            state: 'disconnected'
                        }
                    });
                    document.dispatchEvent(event);

                    // Cleanup global state
                    this.channelStates.delete(source.channel);
                    this.updateBodyAttribute(source.channel, null);

                }, 5000);
            }
        }

        updateSourceState(src, state) {
            if (!src) return;
            const source = this.sources.get(src);
            if (!source && state !== 'disconnected') return;

            // Should be impossible if source is gone, but safety check
            const channel = source ? source.channel : this.getChannelName(src);
            if (!channel) return;

            if (source) source.state = state;

            // Update global trackers
            this.channelStates.set(channel, state);
            this.updateBodyAttribute(channel, state);
            this.syncDependentElements(channel);

            // Sync Islands on connect
            if (state === 'connected') {
                this.syncIslands(channel);
            }

            // Dispatch to all active elements for this src
            if (source) {
                source.elements.forEach(el => {
                    this.dispatchToElement(el, channel, state);
                });
            }
        }

        dispatchToElement(element, channel, state) {
            if (!element || !state) return;

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
            element.dispatchEvent(event);
        }

        getChannelName(src) {
            if (!src) return null;
            try {
                const url = new URL(src, window.location.origin);
                const path = url.pathname.split('/').filter(Boolean).pop() || '';
                return path + url.search + url.hash;
            } catch {
                return null;
            }
        }

        syncDependentElements(targetChannel = null) {
            const selector = targetChannel
                ? `[data-rw-requires-stream="${targetChannel}"]`
                : '[data-rw-requires-stream]';

            const elements = document.querySelectorAll(selector);
            elements.forEach(el => {
                const channel = el.getAttribute('data-rw-requires-stream');
                const state = this.channelStates.get(channel);

                if (state === 'connected' || (state === 'connecting' && el.tagName === 'TURBO-FRAME')) {
                    el.removeAttribute('disabled');
                    el.removeAttribute('aria-disabled');
                } else {
                    el.setAttribute('disabled', 'disabled');
                    el.setAttribute('aria-disabled', 'true');
                }
            });
        }

        updateBodyAttribute(channel, state) {
            const attr = `data-rw-stream-${channel}`;
            if (state && state !== 'disconnected') {
                document.body.setAttribute(attr, state);
            } else {
                document.body.removeAttribute(attr);
            }
        }
    }

    /**
     * LocalTimeFormatter - Formats UTC timestamps to user's local timezone
     * Handles <time data-rw-time> elements with support for:
     * - display: time (default), date, datetime, relative
     * - format: short, medium (default), long, full
     */
    class LocalTimeFormatter {
        constructor() {
            this.observer = new MutationObserver(mutations => this.handleMutations(mutations));
            this.formatter = typeof Intl !== 'undefined' && Intl.RelativeTimeFormat
                ? new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' })
                : null;
            this.updateInterval = null;
            this.isStarted = false;
            this.visibleElements = new Set();
            this.intersectionObserver = typeof IntersectionObserver !== 'undefined'
                ? new IntersectionObserver((entries) => {
                    entries.forEach(entry => {
                        if (entry.isIntersecting) {
                            this.visibleElements.add(entry.target);
                        } else {
                            this.visibleElements.delete(entry.target);
                        }
                    });
                })
                : null;
        }

        start() {
            if (this.isStarted) return;
            this.isStarted = true;

            this.formatAll();
            this.observer.observe(document.body, { childList: true, subtree: true });

            // Re-format on Turbo navigations
            document.addEventListener('turbo:load', () => this.formatAll());
            document.addEventListener('turbo:render', () => {
                this.observer.disconnect();
                this.observer.observe(document.body, { childList: true, subtree: true });
                this.formatAll();
            });

            this.startTimer();
        }

        startTimer() {
            if (this.updateInterval) return;
            // Update every 30 seconds to keep relative times (like "just now") fresh
            // Only updates elements currently visible in the viewport
            this.updateInterval = setInterval(() => this.formatRelativeOnly(), 30000);
        }

        stopTimer() {
            if (this.updateInterval) {
                clearInterval(this.updateInterval);
                this.updateInterval = null;
            }
        }

        handleMutations(mutations) {
            for (const mutation of mutations) {
                for (const node of mutation.addedNodes) {
                    if (node instanceof Element) {
                        const isRelative = (el) => el.tagName === 'TIME' && el.getAttribute('data-rw-time-display') === 'relative';

                        if (node.tagName === 'TIME' && node.hasAttribute('data-rw-time')) {
                            this.format(node);
                            if (isRelative(node) && this.intersectionObserver) this.intersectionObserver.observe(node);
                        }
                        node.querySelectorAll('time[data-rw-time]').forEach(el => {
                            this.format(el);
                            if (isRelative(el) && this.intersectionObserver) this.intersectionObserver.observe(el);
                        });
                    }
                }
            }
        }

        formatAll() {
            this.visibleElements.clear();
            if (this.intersectionObserver) {
                this.intersectionObserver.disconnect();
            }

            document.querySelectorAll('time[data-rw-time]').forEach(el => {
                this.format(el);
                if (el.getAttribute('data-rw-time-display') === 'relative' && this.intersectionObserver) {
                    this.intersectionObserver.observe(el);
                }
            });
        }

        formatRelativeOnly() {
            if (this.intersectionObserver) {
                // Optimized update: only target elements that are relative AND visible in viewport
                this.visibleElements.forEach(el => {
                    this.format(el);
                });
            } else {
                // Fallback for environments without IntersectionObserver support: update all relative elements
                const selector = 'time[data-rw-time][data-rw-time-display="relative"]';
                document.querySelectorAll(selector).forEach(el => {
                    this.format(el);
                });
            }
        }

        format(element) {
            const dateStr = element.getAttribute('datetime');
            if (!dateStr) return;

            const date = new Date(dateStr);
            if (isNaN(date.getTime())) return;

            const display = element.getAttribute('data-rw-time-display') || 'time';
            const formatStyle = element.getAttribute('data-rw-time-format') || 'medium';

            let text = '';
            if (display === 'relative') {
                text = this.getRelativeTime(date);
            } else if (display === 'date') {
                text = date.toLocaleDateString(undefined, { dateStyle: formatStyle });
            } else if (display === 'datetime') {
                text = date.toLocaleString(undefined, { dateStyle: formatStyle, timeStyle: formatStyle });
            } else {
                // Default to time
                text = date.toLocaleTimeString(undefined, { timeStyle: formatStyle });
            }

            if (text) {
                element.textContent = text;
            }
        }

        getRelativeTime(date) {
            const now = Date.now();
            const diff = date.getTime() - now;
            const absDiff = Math.abs(diff);
            const seconds = Math.round(diff / 1000);
            const minutes = Math.round(diff / 60000);
            const hours = Math.round(diff / 3600000);
            const days = Math.round(diff / 86400000);

            // Use Intl.RelativeTimeFormat if available
            if (this.formatter) {
                // Special handling for "just now" / "in a moment" to match user preference
                // Intl typically returns "in 0 seconds" or "0 seconds ago"
                if (absDiff < 60000) {
                    return diff >= 0 ? 'in a moment' : 'just now';
                }

                if (Math.abs(minutes) < 60) return this.formatter.format(minutes, 'minute');
                if (Math.abs(hours) < 24) return this.formatter.format(hours, 'hour');
                return this.formatter.format(days, 'day');
            }

            // Fallback for environments without Intl support
            const abs = Math.abs;
            if (abs(seconds) < 60) return diff >= 0 ? 'in a moment' : 'just now';
            if (abs(minutes) < 60) return minutes >= 0 ? `in ${minutes} min` : `${abs(minutes)} min ago`;
            if (abs(hours) < 24) return hours >= 0 ? `in ${hours} hr` : `${abs(hours)} hr ago`;
            return days >= 0 ? `in ${days} days` : `${abs(days)} days ago`;
        }


    }

    // Initialize
    const connectionManager = new ConnectionManager();
    const localTimeFormatter = new LocalTimeFormatter();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            connectionManager.start();
            localTimeFormatter.start();
        });
    } else {
        connectionManager.start();
        localTimeFormatter.start();
    }

    window.RazorWire = { connectionManager, localTimeFormatter };
    // Global safeguard: Block clicks on disabled elements or their children even if pointer-events are enabled
    document.addEventListener('click', (e) => {
        const selector = '[disabled], [aria-disabled="true"], [data-rw-requires-stream][disabled]';

        let target = e.target;
        if (!(target instanceof Element) && target.parentElement) {
            target = target.parentElement;
        }

        if (target instanceof Element) {
            const disabledElement = target.closest(selector);
            if (disabledElement) {
                e.preventDefault();
                e.stopPropagation();
            }
        }
    }, true); // Capture phase to intervene early

    console.log('✅ RazorWire Runtime Initialized');
})();
