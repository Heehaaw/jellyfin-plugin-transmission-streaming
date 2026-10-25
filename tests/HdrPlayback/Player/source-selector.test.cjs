// Run from the repository root: node --test tests/HdrPlayback/Player/*.test.cjs
// Exercises the selector adapter against native-style source changes that rebuild audio/subtitle options.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const script = fs.readFileSync(path.join(__dirname, '../../../src/HdrPlayback/Jellyfin.Plugin.HdrPlayback/Player/source-selector.js'), 'utf8');

function environment(eligible = true) {
    const originalId = 'original-media-source';
    const convertedId = `hdr-conversion:${originalId}`;
    let observed;
    let textChanges = 0;
    const pending = [];
    const flush = () => {
        for (let count = 0; pending.length; count++) {
            assert.ok(count < 20, 'the details page must settle instead of starving the UI with mutation callbacks');
            pending.shift()();
        }
    };
    const option = (value, text) => {
        const result = { value, dataset: {} };
        Object.defineProperty(result, 'textContent', {
            get: () => text,
            set: value => { text = value; textChanges++; observed?.(); }
        });
        return result;
    };
    const video = {
        options: [option('0', '1080p H264')], value: '0', disabled: true,
        classList: { contains: name => name === 'selectVideo' },
        add(value) { this.options.push(value); },
        hasAttribute(name) { return name === 'disabled' && this.disabled; },
        removeAttribute(name) { if (name === 'disabled') { this.disabled = false; observed?.(); } },
        closest() { return form; }
    };
    const audio = { options: [option('1'), option('2')], value: '2' };
    const subtitles = { options: [option('-1'), option('3')], value: '3' };
    const version = { hidden: false, classList: { toggle(name, value) { version.hidden = value; } } };
    const sources = {
        options: [option(originalId, 'Original'), ...(eligible ? [option(convertedId, 'HDR (converted)')] : [])], value: originalId,
        dispatchEvent() {
            video.options = [option('0', '1080p H264')]; video.value = '0'; video.disabled = true; observed?.();
            audio.value = '1'; subtitles.value = '-1';
        }
    };
    const form = { querySelector: selector => ({ '.selectSource': sources, '.selectVideo': video, '.selectAudio': audio, '.selectSubtitles': subtitles, '.selectSourceContainer': version })[selector] };
    let change;
    const document = {
        documentElement: {},
        addEventListener(name, handler) { if (name === 'change') change = handler; },
        querySelectorAll: () => [form],
        createElement: () => option('')
    };
    vm.runInNewContext(script, { window: {}, document, MutationObserver: class { constructor(callback) { observed = callback; } observe() {} }, queueMicrotask: callback => pending.push(callback), Event });
    flush();
    return { sources, video, audio, subtitles, version, originalId, convertedId, textChanges: () => textChanges, select: value => { video.value = value; change({ target: video }); flush(); } };
}

test('adds HDR beside the existing video name without inventing a physical track', () => {
    const env = environment();
    assert.equal(env.video.options[0].textContent, '1080p H264');
    assert.equal(env.video.options[1].textContent, 'HDR (converted)');
    assert.equal(env.video.disabled, false);
    assert.equal(env.sources.value, env.originalId);
    assert.equal(env.version.hidden, true);
});

test('HDR and Original route through native source selection and preserve selected audio and subtitles', () => {
    const env = environment();
    env.select(env.convertedId);
    assert.equal(env.sources.value, env.convertedId);
    assert.equal(env.video.value, env.convertedId);
    assert.equal(env.audio.value, '2');
    assert.equal(env.subtitles.value, '3');
    env.select('0');
    assert.equal(env.sources.value, env.originalId);
    assert.equal(env.audio.value, '2');
    assert.equal(env.subtitles.value, '3');
    assert.equal(env.video.options.length, 2);
});

test('keeps native video display disabled when no conversion source is offered', () => {
    const env = environment(false);
    assert.equal(env.video.options.length, 1);
    assert.equal(env.video.disabled, true);
});


test('native details mutations settle after inserting the converted choice', () => {
    const env = environment();
    assert.equal(env.textChanges(), 1);
    env.select(env.convertedId);
    assert.equal(env.textChanges(), 2);
});
