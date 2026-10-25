// Run from the repository root: node --test tests/HdrPlayback/Player/player.test.cjs
// Uses Node's built-in runner and VM. CI runs it in .github/workflows/build.yml separately from dotnet test.

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../../src/HdrPlayback/Jellyfin.Plugin.HdrPlayback/Player/player.js'), 'utf8');
const itemId = '57fa5792d4d64de8b357fb97c516dbb9';

function environment() {
    const requests = [];
    let available = true;
    let selectedSource = false;
    class Xhr {
        open(method, url) { this.url = url; }
        setRequestHeader(key, value) { (this.headers ||= {})[key] = value; }
        addEventListener(name, callback) { this.loaded = callback; }
        getResponseHeader(name) { return name === 'X-Jellyfin-Hdr-Available' ? String(available) : name === 'X-Jellyfin-Hdr-Source' ? String(selectedSource) : 'Hardware unavailable'; }
        send() { requests.push(this); this.loaded?.(); }
    }
    const window = {
        location: { href: 'http://jellyfin/web/', origin: 'http://jellyfin' },
        XMLHttpRequest: Xhr,
        alert(message) { throw new Error(message); },
        fetch: async (url, init) => {
            requests.push({ url, init });
            return new Response('{}', { headers: { 'X-Jellyfin-Hdr-Available': String(available), 'X-Jellyfin-Hdr-Reason': 'Hardware unavailable', 'X-Jellyfin-Hdr-Source': String(selectedSource) } });
        }
    };
    const context = vm.createContext({ window, XMLHttpRequest: Xhr, URL, Headers, Request, Response, console });
    vm.runInContext(source, context);
    let current = itemId;
    const restarts = [];
    const manager = {
        currentItem: () => ({ Id: current }),
        getMaxStreamingBitrate: () => 20000000,
        enableAutomaticBitrateDetection: () => false,
        setMaxStreamingBitrate: options => restarts.push(options)
    };
    const player = {};
    return { window, requests, manager, player, restarts, setAvailability: value => { available = value; }, setSelectedSource: value => { selectedSource = value; }, setCurrent: value => { current = value; } };
}

test('HDR remains off until selected and preserves quality on restart', async () => {
    const env = environment();
    const url = `http://jellyfin/Items/${itemId}/PlaybackInfo`;
    await env.window.fetch(url);
    assert.equal(env.window.JellyfinHdrPlayback.menu(env.manager, env.player).asideText, 'Off');
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    await env.window.fetch(url);
    assert.equal(env.requests.at(-1).init.headers.get('X-Jellyfin-Hdr-Conversion'), 'true');
    assert.equal(env.restarts.length, 1);
    assert.equal(env.restarts[0].maxBitrate, env.manager.getMaxStreamingBitrate());
    assert.equal(env.restarts[0].enableAutomaticBitrateDetection, false);
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    await env.window.fetch(url);
    assert.equal(env.requests.at(-1).init, undefined);
});

test('native quality renegotiations keep HDR mode and retain request content', async () => {
    const env = environment();
    const url = `http://jellyfin/Items/${itemId}/PlaybackInfo`;
    await env.window.fetch(url);
    env.window.JellyfinHdrPlayback.menu(env.manager, env.player);
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    const body = JSON.stringify({ MaxStreamingBitrate: 4000000, StartTimeTicks: 123456789, AudioStreamIndex: 2 });
    await env.window.fetch(url, { method: 'POST', body });
    assert.equal(env.requests.at(-1).init.body, body);
    assert.equal(env.requests.at(-1).init.headers.get('X-Jellyfin-Hdr-Conversion'), 'true');
});

test('XMLHttpRequest negotiations receive the same mode without marking unrelated or foreign URLs', async () => {
    const env = environment();
    const url = `http://jellyfin/Items/${itemId}/PlaybackInfo`;
    const first = new env.window.XMLHttpRequest();
    first.open('POST', url); first.send('{}');
    env.window.JellyfinHdrPlayback.menu(env.manager, env.player);
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    const hdr = new env.window.XMLHttpRequest();
    hdr.open('POST', url); hdr.send('{}');
    assert.equal(hdr.headers['X-Jellyfin-Hdr-Conversion'], 'true');
    await env.window.fetch(`http://other-server/Items/${itemId}/PlaybackInfo`);
    assert.equal(env.requests.at(-1).init, undefined);
    await env.window.fetch('http://jellyfin/System/Info/Public');
    assert.equal(env.requests.at(-1).init, undefined);
});

test('a new movie and playback stop clear the selected mode', async () => {
    const env = environment();
    const url = `http://jellyfin/Items/${itemId}/PlaybackInfo`;
    await env.window.fetch(url);
    env.window.JellyfinHdrPlayback.menu(env.manager, env.player);
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    await env.window.fetch('http://jellyfin/Sessions/Playing/Stopped', { method: 'POST' });
    await env.window.fetch(url);
    assert.equal(env.requests.at(-1).init, undefined);
    env.window.JellyfinHdrPlayback.menu(env.manager, env.player);
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    env.setCurrent('0936d9d4302146fc8b2bf7c1e462b38a');
    await env.window.fetch(url);
    assert.equal(env.requests.at(-1).init, undefined);
});

test('unavailable conversion explains the reason without starting a transcode', async () => {
    const env = environment();
    env.setAvailability(false);
    const url = `http://jellyfin/Items/${itemId}/PlaybackInfo`;
    await env.window.fetch(url);
    let reason;
    env.window.alert = message => { reason = message; };
    assert.equal(env.window.JellyfinHdrPlayback.menu(env.manager, env.player).asideText, 'Unavailable');
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    assert.equal(reason, 'Hardware unavailable');
    assert.equal(env.restarts.length, 0);
});

test('selected HDR can be turned off after availability changes', async () => {
    const env = environment();
    const url = `http://jellyfin/Items/${itemId}/PlaybackInfo`;
    await env.window.fetch(url);
    env.window.JellyfinHdrPlayback.menu(env.manager, env.player);
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    env.setAvailability(false);
    await env.window.fetch(url);
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    await env.window.fetch(url);
    assert.equal(env.requests.at(-1).init, undefined);
    assert.equal(env.restarts.length, 2);
});


test('a selected HDR video version hides the settings toggle and returning to Original restores it', async () => {
    const env = environment();
    env.setSelectedSource(true);
    await env.window.fetch(`http://jellyfin/Items/${itemId}/PlaybackInfo`);
    let reason;
    env.window.alert = message => { reason = message; };
    assert.equal(env.window.JellyfinHdrPlayback.sourceSelected(env.manager, env.player), true);
    await env.window.JellyfinHdrPlayback.toggle(env.manager, env.player);
    assert.match(reason, /original video version/);
    assert.equal(env.restarts.length, 0);
    env.setSelectedSource(false);
    await env.window.fetch(`http://jellyfin/Items/${itemId}/PlaybackInfo`);
    assert.equal(env.window.JellyfinHdrPlayback.sourceSelected(env.manager, env.player), false);
});
