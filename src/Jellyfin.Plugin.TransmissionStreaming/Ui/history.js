import {read, textElement} from './ConfigurationPage?name=transmission-streaming-elements.js';

function historyItem(entry) {
    const row = textElement('li', 'ts-entry', '');
    const title = read(entry, 'title') ?? 'Untitled item';
    const nativeId = read(entry, 'nativeItemId');
    const label = textElement(nativeId ? 'a' : 'span', 'ts-entry-title', title);
    if (nativeId) {
        // Stay within the current web app, including its reverse-proxy base path and selected server.
        label.href = '#/details?id=' + encodeURIComponent(nativeId) + '&serverId=' + encodeURIComponent(ApiClient.serverId());
        label.dataset.tsTooltip = 'Open the native Jellyfin item used after the handoff.';
        label.setAttribute('aria-description', label.dataset.tsTooltip);
    }
    const time = textElement('time', 'ts-entry-detail', 'Completion time unavailable');
    const completedAt = read(entry, 'completedAt');
    const completed = completedAt ? new Date(completedAt) : null;
    if (completed && !Number.isNaN(completed.getTime())) {
        time.dateTime = completed.toISOString();
        time.textContent = completed.toLocaleString();
    }
    row.append(label, time);
    return row;
}

export default class CompletionHistory {
    constructor(page) {
        this.page = page;
        this.render([]);
        const heading = this.field('completedHistoryHeading');
        heading.dataset.tsTooltip = 'Recent successful handoffs from progressive streaming to native Jellyfin playback.';
        heading.setAttribute('aria-description', heading.dataset.tsTooltip);
    }

    field(name) { return this.page.querySelector('#' + name); }

    render(entries) {
        this.field('completedHistoryCount').textContent = entries.length + (entries.length === 1 ? ' handoff' : ' handoffs');
        this.field('completedHistoryEntries').replaceChildren(...entries.map(historyItem));
        this.field('completedHistoryEmpty').hidden = entries.length > 0;
    }
}
