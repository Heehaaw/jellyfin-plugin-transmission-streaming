import {buttonText, read, textElement} from './ConfigurationPage?name=transmission-streaming-elements.js';
import CompletionHistory from './ConfigurationPage?name=transmission-streaming-history.js';
import {explanations} from './ConfigurationPage?name=transmission-streaming-tooltips.js';

function displayDate(value) {
    if (!value) return 'Not yet reported';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? 'Not yet reported' : date.toLocaleString();
}

function renderEntry(entry) {
    const row = textElement('li', 'ts-entry', '');
    const heading = textElement('div', 'ts-row-heading', '');
    const title = read(entry, 'title') ?? 'Untitled item';
    heading.append(textElement('span', 'ts-entry-title', title), textElement('span', 'ts-count', read(entry, 'status') ?? 'Unknown'));
    const progress = document.createElement('progress');
    const fraction = Number(read(entry, 'progress'));
    progress.max = 1;
    progress.value = Number.isFinite(fraction) ? Math.max(0, Math.min(1, fraction)) : 0;
    progress.setAttribute('aria-label', title + ': download progress');
    const readers = Math.max(0, Number(read(entry, 'readers')) || 0);
    row.append(heading, progress, textElement('p', 'ts-entry-detail', Math.round(progress.value * 100) + '% downloaded · ' + readers + (readers === 1 ? ' active reader' : ' active readers')));
    return row;
}

export default class Activity {
    constructor(page, lifecycle) {
        this.page = page;
        this.lifecycle = lifecycle;
        this.busy = false;
        this.history = new CompletionHistory(page);
        this.refreshButton = this.field('refreshStreamingStatus');
        buttonText(this.refreshButton, 'Refresh', explanations.refresh);
        this.refreshButton.addEventListener('click', () => this.refresh());
    }

    field(name) { return this.page.querySelector('#' + name); }

    render(result) {
        const entries = read(result, 'entries') ?? [];
        this.history.render(read(result, 'completed') ?? []);
        const enabled = Boolean(read(result, 'enabled'));
        this.field('streamingStatus').textContent = enabled ? 'Scanning enabled' : 'Scanning disabled';
        this.field('lastSuccessfulPoll').textContent = displayDate(read(result, 'lastSuccessfulPoll'));
        this.field('streamingItemCount').textContent = entries.length;
        this.field('streamingReaderCount').textContent = entries.reduce((total, entry) => total + Math.max(0, Number(read(entry, 'readers')) || 0), 0);
        this.field('streamingStatusMessage').textContent = (read(result, 'lastFailure') ? 'Last download scan failed. Check the server log. ' : '') + 'Status checked ' + new Date().toLocaleTimeString() + '.';
        this.field('streamingEntries').replaceChildren(...entries.map(renderEntry));
        this.field('streamingEmpty').hidden = entries.length > 0;
        this.field('streamingEmpty').textContent = enabled ? 'No streaming items yet. Start an arr-managed download; it will appear after the next successful scan.' : 'Scanning is disabled. Enable the progressive library and save to expose managed downloads here.';
    }

    stop() { clearTimeout(this.timer); }

    schedule(delay = 10000) {
        this.stop();
        if (this.lifecycle.active) this.timer = setTimeout(() => this.refresh(), delay);
    }

    async refresh() {
        if (!this.lifecycle.active || this.busy) return;
        this.stop();
        const generation = this.lifecycle.generation;
        this.busy = true;
        this.refreshButton.disabled = true;
        buttonText(this.refreshButton, 'Refreshing…');
        try {
            const result = await ApiClient.getJSON(ApiClient.getUrl('TransmissionStreaming/status'));
            if (this.lifecycle.active && generation === this.lifecycle.generation) this.render(result);
        } catch {
            if (this.lifecycle.active && generation === this.lifecycle.generation) this.field('streamingStatusMessage').textContent = 'Status unavailable. Values may be out of date. Retry with Refresh or check the server log.';
        } finally {
            this.busy = false;
            this.refreshButton.disabled = false;
            buttonText(this.refreshButton, 'Refresh');
            if (this.lifecycle.active) this.schedule(generation === this.lifecycle.generation ? 10000 : 0);
        }
    }
}
