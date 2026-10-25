(function () {
    'use strict';
    if (window.JellyfinHdrPlayback) return;

    const availability = new Map();
    let active = null;
    const normalize = value => String(value || '').replace(/-/g, '').toLowerCase();

    function playbackItem(url) {
        try {
            const parsed = new URL(typeof url === 'string' ? url : url.url, window.location.href);
            if (parsed.origin !== window.location.origin) return null;
            const match = parsed.pathname.match(/\/Items\/([a-f\d-]+)\/PlaybackInfo$/i);
            return match ? normalize(match[1]) : null;
        } catch (_) {
            return null;
        }
    }

    function isEnabled(itemId) {
        if (!active || !itemId) return false;
        const current = active.manager.currentItem(active.player);
        if (normalize(current && current.Id) !== active.itemId) {
            active = null;
            return false;
        }
        return active.itemId === itemId && active.enabled;
    }

    function observe(itemId, getHeader) {
        if (!itemId) return;
        const reported = getHeader('X-Jellyfin-Hdr-Available');
        if (reported !== null) {
            availability.set(itemId, {
                available: reported === 'true',
                reason: getHeader('X-Jellyfin-Hdr-Reason') || ''
            });
        }
    }

    function trackStop(url) {
        const path = typeof url === 'string' ? url : url.url;
        if (/\/Sessions\/Playing\/Stopped(?:\?|$)/i.test(path)) active = null;
    }

    // Jellyfin's generated SDK may use fetch or XMLHttpRequest. Mark only its normal, same-server negotiation requests.
    if (window.fetch) {
        const originalFetch = window.fetch;
        window.fetch = function (input, init) {
            const itemId = playbackItem(input);
            trackStop(input);
            if (isEnabled(itemId)) {
                const headers = new Headers((init && init.headers) || (input instanceof Request ? input.headers : undefined));
                headers.set('X-Jellyfin-Hdr-Conversion', 'true');
                init = Object.assign({}, init, { headers });
            }
            return originalFetch.call(this, input, init).then(response => {
                observe(itemId, name => response.headers.get(name));
                return response;
            });
        };
    }

    if (window.XMLHttpRequest) {
        const originalOpen = XMLHttpRequest.prototype.open;
        const originalSend = XMLHttpRequest.prototype.send;
        XMLHttpRequest.prototype.open = function (method, url) {
            this.hdrPlaybackUrl = url;
            return originalOpen.apply(this, arguments);
        };
        XMLHttpRequest.prototype.send = function () {
            const itemId = playbackItem(this.hdrPlaybackUrl);
            trackStop(this.hdrPlaybackUrl);
            if (isEnabled(itemId)) this.setRequestHeader('X-Jellyfin-Hdr-Conversion', 'true');
            this.addEventListener('load', () => observe(itemId, name => this.getResponseHeader(name)), { once: true });
            return originalSend.apply(this, arguments);
        };
    }

    window.JellyfinHdrPlayback = {
        menu(manager, player) {
            const item = manager.currentItem(player);
            const itemId = normalize(item && item.Id);
            if (!active || active.itemId !== itemId || active.player !== player) {
                active = { manager, player, itemId, enabled: false };
            }
            const status = availability.get(itemId);
            return {
                name: 'HDR conversion',
                id: 'hdr-playback',
                asideText: active.enabled ? 'On' : (status && !status.available ? 'Unavailable' : 'Off')
            };
        },
        toggle(manager, player) {
            this.menu(manager, player);
            const status = availability.get(active.itemId);
            if (!active.enabled && (!status || !status.available)) {
                window.alert(status ? status.reason : 'HDR availability has not been reported for this playback.');
                return Promise.resolve();
            }
            active.enabled = !active.enabled;
            // Native renegotiation retains position, selected tracks and the current quality setting, and allocates a fresh play session.
            manager.setMaxStreamingBitrate({
                enableAutomaticBitrateDetection: manager.enableAutomaticBitrateDetection(player),
                maxBitrate: manager.getMaxStreamingBitrate(player)
            }, player);
            return Promise.resolve();
        }
    };
})();
