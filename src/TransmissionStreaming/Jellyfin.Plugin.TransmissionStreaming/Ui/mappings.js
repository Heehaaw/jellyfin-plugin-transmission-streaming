import {button, buttonText, read, textElement} from './ConfigurationPage?name=transmission-streaming-elements.js';
import {explanations, help, roleHelp} from './ConfigurationPage?name=transmission-streaming-tooltips.js';

const remoteKey = path => path.trim().replace(/\/+$/, '');
const absoluteDirectory = path => path.startsWith('/') && remoteKey(path).length > 0
    && !/[\\\0\r\n]/.test(path) && !path.split('/').some(part => part === '.' || part === '..');

export default class PathMappings {
    constructor(page, lifecycle, onEdit) {
        this.page = page;
        this.lifecycle = lifecycle;
        this.onEdit = onEdit;
        this.nextRowId = 0;
        this.services = [{name: 'Transmission', value: 0}, {name: 'Radarr', value: 1}, {name: 'Sonarr', value: 2}];
        this.services.forEach(service => this.initialize(service));
    }

    field(name) { return this.page.querySelector('#' + name); }

    purpose(service, remote) {
        if (service.value === 1) return 'Movie library';
        if (service.value === 2) return 'Series library';
        if (remote.trim() && remoteKey(remote) === remoteKey(read(service.paths, 'incompleteDirectory') ?? '')) return 'Incomplete downloads';
        if (remote.trim() && remoteKey(remote) === remoteKey(read(service.paths, 'downloadDirectory') ?? '')) return 'Default downloads';
        return (read(service.paths, 'downloadDirectories') ?? []).some(path => remoteKey(path) === remoteKey(remote)) ? 'Custom download folder' : 'Mapped folder';
    }

    summarize(service) {
        const count = service.rows.children.length;
        const counter = this.field(service.name + 'MappingCount');
        counter.textContent = count + (count === 1 ? ' mapping' : ' mappings');
        help(counter, 'Translation rows for this service, including unsaved edits. Blank rows are skipped when saving.');
        service.empty.hidden = count > 0;
        for (const row of service.rows.children) {
            const [remote, local] = row.querySelectorAll('input');
            const role = this.purpose(service, remote.value);
            const label = row.querySelector('.ts-purpose');
            label.textContent = role;
            roleHelp(label, role);
            const missing = row.querySelector('.ts-map-state');
            missing.textContent = local.value.trim() ? '' : 'Needs Jellyfin path';
            help(missing, explanations.missingPath);
        }
    }

    input(labelText, value, explanation) {
        // The native input needs its parent during upgrade. Only constant markup is parsed here.
        const container = document.createElement('div');
        container.className = 'inputContainer';
        container.innerHTML = '<label class="inputLabel"></label><input is="emby-input" type="text" autocomplete="off">';
        const label = container.querySelector('label');
        const input = container.querySelector('input');
        input.id = 'streamingMapping' + this.nextRowId++;
        input.value = value ?? '';
        input.setAttribute('aria-description', explanation);
        input.dataset.tsTooltip = explanation;
        label.htmlFor = input.id;
        label.textContent = labelText;
        help(label, explanation);
        input.addEventListener('input', () => input.removeAttribute('aria-invalid'));
        return container;
    }

    add(service, remoteRoot = '', localRoot = '') {
        const row = textElement('div', 'streamingMapping', '');
        const heading = textElement('div', 'ts-row-heading', '');
        heading.append(textElement('span', 'ts-purpose', ''), textElement('span', 'ts-map-state', ''));
        const remove = button('Remove mapping', () => {
            row.remove();
            service.edited = true;
            this.summarize(service);
            this.onEdit();
        }, explanations.remove);
        remove.classList.add('ts-map-remove');
        const pair = textElement('div', 'ts-map-pair', '');
        const arrow = textElement('span', 'ts-arrow', '→');
        arrow.setAttribute('aria-hidden', 'true');
        pair.append(this.input('Directory in ' + service.name, remoteRoot, 'Absolute folder path as seen by ' + service.name + '.'),
            arrow, this.input('Same directory in Jellyfin', localRoot, explanations.localPath), remove);
        row.append(heading, pair);
        service.rows.append(row);
        this.summarize(service);
        return row;
    }

    merge(service, mappings) {
        const existing = new Set(Array.from(service.rows.children, row => remoteKey(row.querySelector('input').value)));
        let added = 0;
        for (const mapping of mappings) {
            const remote = read(mapping, 'remoteRoot') ?? '';
            if (!remote.trim() || existing.has(remoteKey(remote))) continue;
            this.add(service, remote, read(mapping, 'localRoot'));
            existing.add(remoteKey(remote));
            added++;
        }
        this.summarize(service);
        return added;
    }

    connection(service) {
        return {
            Url: this.field(service.name + 'Url').value.trim(),
            Username: service.value === 0 ? this.field('TransmissionUsername').value : '',
            Password: service.value === 0 ? this.field('TransmissionPassword').value : '',
            ApiKey: service.value === 0 ? '' : this.field(service.name + 'ApiKey').value
        };
    }

    async discover(service) {
        const credentials = this.connection(service);
        if (!credentials.Url) {
            service.message.textContent = 'Enter the service URL before discovering paths.';
            return;
        }
        const generation = this.lifecycle.generation;
        service.discover.disabled = true;
        buttonText(service.discover, 'Discovering…');
        service.message.textContent = 'Checking folders in ' + service.name + '…';
        try {
            const result = await ApiClient.ajax({
                type: 'POST', url: ApiClient.getUrl('TransmissionStreaming/configuration/paths/' + service.name),
                data: JSON.stringify(credentials), dataType: 'json', contentType: 'application/json'
            });
            if (!this.lifecycle.active || generation !== this.lifecycle.generation) return;
            service.paths = read(result, 'paths');
            const suggestions = read(result, 'mappings') ?? [];
            const added = this.merge(service, suggestions);
            if (added) { service.edited = true; this.onEdit(); }
            service.message.textContent = 'Folders checked ' + new Date().toLocaleTimeString() + ' · ' + suggestions.length + ' mapping suggestion(s) · ' + added + ' added. '
                + (suggestions.length ? 'Existing edits were kept. Verify local paths before saving.' : 'Add a mapping manually if needed.');
        } catch {
            // Upstream responses may contain credentials. Display only this fixed explanation.
            if (this.lifecycle.active && generation === this.lifecycle.generation) service.message.textContent = 'Could not discover paths. Check the URL, credentials and connectivity. Your mappings are unchanged.';
        } finally {
            service.discover.disabled = false;
            buttonText(service.discover, 'Discover folders');
            if (generation !== this.lifecycle.generation) service.message.textContent = 'Discover again to refresh the folder suggestions.';
        }
    }

    initialize(service) {
        const actions = textElement('div', 'ts-heading ts-mapping-heading', '');
        service.rows = document.createElement('div');
        service.message = textElement('p', 'ts-service-message ts-muted', 'Folders not checked in this visit.');
        service.message.setAttribute('role', 'status');
        service.message.setAttribute('aria-live', 'polite');
        service.discover = button('Discover folders', () => this.discover(service), explanations.discover);
        this.field(service.name + 'Connection').append(service.discover);
        actions.append(textElement('h4', '', 'Folder translations'), button('Add mapping', () => {
            this.add(service);
            service.edited = true;
            this.onEdit();
        }, explanations.add));
        const explanation = textElement('p', 'ts-map-intro ts-muted', 'Translate each folder into Jellyfin’s filesystem. Multiple folders can use different mounts. Removing a mapping never deletes files.');
        service.empty = textElement('p', 'ts-map-intro ts-muted', 'No mappings yet. Discover folders or add one manually. A shared fallback applies only when this service has no mappings.');
        this.field(service.name + 'Paths').append(actions, explanation, service.message, service.rows, service.empty);
        service.rows.addEventListener('input', () => { service.edited = true; this.summarize(service); });
    }

    load(config) {
        for (const service of this.services) {
            if (service.edited) continue;
            service.rows.replaceChildren();
            for (const mapping of read(config, 'pathMappings') ?? []) {
                const owner = read(mapping, 'service');
                if (Number(owner) === service.value || owner === service.name) this.add(service, read(mapping, 'remoteRoot'), read(mapping, 'localRoot'));
            }
            this.summarize(service);
        }
    }

    draft() {
        const mappings = [];
        for (const service of this.services) {
            let rowNumber = 0;
            for (const row of service.rows.children) {
                rowNumber++;
                const [remote, local] = row.querySelectorAll('input');
                if (!remote.value.trim() && !local.value.trim()) continue;
                for (const input of [remote, local]) {
                    if (!absoluteDirectory(input.value.trim())) {
                        input.setAttribute('aria-invalid', 'true');
                        input.focus();
                        this.field('configurationError').textContent = service.name + ' mapping ' + rowNumber + ': enter absolute remote and Jellyfin directories, or remove the row.';
                        return null;
                    }
                }
                mappings.push({Service: service.value, RemoteRoot: remote.value.trim(), LocalRoot: local.value.trim()});
            }
        }
        return mappings;
    }
}
