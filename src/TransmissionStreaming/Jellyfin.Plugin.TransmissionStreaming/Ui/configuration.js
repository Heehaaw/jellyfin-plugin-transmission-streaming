import {buttonText, read} from './ConfigurationPage?name=transmission-streaming-elements.js';
import {explanations} from './ConfigurationPage?name=transmission-streaming-tooltips.js';

const fields = ['Enabled', 'TransmissionUrl', 'TransmissionUsername', 'TransmissionPassword', 'RadarrUrl', 'RadarrApiKey', 'SonarrUrl', 'SonarrApiKey',
    'RemoteMediaRoot', 'LocalMediaRoot', 'ProjectionRoot', 'JellyfinPublicUrl', 'PollSeconds', 'PieceWaitSeconds', 'PrioritizePlayback', 'ShowDownloadStatus'];

function connectedServerUrl() {
    const suffix = '/TransmissionStreaming/status';
    const endpoint = new URL(ApiClient.getUrl('TransmissionStreaming/status'), window.location.href);
    if (!['http:', 'https:'].includes(endpoint.protocol) || endpoint.username || endpoint.password || !endpoint.pathname.endsWith(suffix)) return '';
    endpoint.pathname = endpoint.pathname.slice(0, -suffix.length);
    endpoint.search = '';
    endpoint.hash = '';
    return endpoint.href.replace(/\/$/, '');
}

export default class ConfigurationForm {
    constructor(page, lifecycle, mappings, onSaved) {
        this.page = page;
        this.lifecycle = lifecycle;
        this.mappings = mappings;
        this.onSaved = onSaved;
        this.pluginId = page.dataset.pluginId;
        this.editedFields = new Set();
        this.loaded = false;
        this.revision = 0;
        this.savedRevision = 0;
        this.saving = false;
        this.saveButton = this.field('saveStreamingConfiguration');
        buttonText(this.saveButton, 'Save changes', explanations.save);
        const form = this.field('transmissionStreamingForm');
        const edited = event => {
            if (fields.includes(event.target.id)) this.editedFields.add(event.target.id);
            this.markDirty();
        };
        form.addEventListener('input', edited);
        form.addEventListener('change', edited);
        form.addEventListener('submit', event => this.save(event));
    }

    field(name) { return this.page.querySelector('#' + name); }

    showState(message) {
        this.saveButton.disabled = !this.loaded || this.saving;
        this.field('configurationState').textContent = message ?? (this.revision !== this.savedRevision ? 'Unsaved changes' : 'All changes saved');
    }

    markDirty() {
        this.revision++;
        this.showState();
    }

    fillEmpty(name, value) {
        const element = this.field(name);
        if (!this.editedFields.has(name) && !element.value.trim() && value) {
            element.value = value;
            this.markDirty();
        }
    }

    async prefill(generation) {
        this.fillEmpty('JellyfinPublicUrl', connectedServerUrl());
        try {
            const defaults = await ApiClient.getJSON(ApiClient.getUrl('TransmissionStreaming/configuration/defaults'));
            if (!this.lifecycle.active || generation !== this.lifecycle.generation) return;
            this.fillEmpty('ProjectionRoot', read(defaults, 'projectionRoot'));
            this.fillEmpty('LocalMediaRoot', read(defaults, 'localMediaRoot'));
        } catch { /* Local suggestions are optional; the form stays editable. */ }
    }

    async load(generation) {
        this.showState('Loading configuration…');
        try {
            const config = await ApiClient.getPluginConfiguration(this.pluginId);
            if (!this.lifecycle.active || generation !== this.lifecycle.generation) return;
            for (const name of fields) {
                if (this.editedFields.has(name)) continue;
                const element = this.field(name);
                const value = read(config, name[0].toLowerCase() + name.slice(1));
                if (element.type === 'checkbox') element.checked = Boolean(value);
                else element.value = value ?? '';
            }
            this.mappings.load(config);
            this.loaded = true;
            this.showState();
            this.field('configurationError').textContent = '';
            await this.prefill(generation);
            if (this.lifecycle.active && generation === this.lifecycle.generation) this.showState();
        } catch {
            if (this.lifecycle.active && generation === this.lifecycle.generation) {
                this.showState('Configuration unavailable');
                this.field('configurationError').textContent = 'Could not load configuration. Reopen this page before saving.';
            }
        }
    }

    async save(event) {
        event.preventDefault();
        const error = this.field('configurationError');
        error.textContent = '';
        if (!this.loaded) { error.textContent = 'Reload configuration before saving.'; return; }
        if (this.saving) return;
        const mappings = this.mappings.draft();
        if (mappings === null) return;
        // Capture before any request. Edits made while saving remain a newer, unsaved revision.
        const draft = Object.fromEntries(fields.map(name => {
            const element = this.field(name);
            return [name, element.type === 'checkbox' ? element.checked : element.type === 'number' ? Number(element.value) : element.value];
        }));
        const savingRevision = this.revision;
        this.saving = true;
        this.showState('Saving changes…');
        try {
            const config = await ApiClient.getPluginConfiguration(this.pluginId);
            Object.assign(config, draft, {PathMappings: mappings});
            await ApiClient.updatePluginConfiguration(this.pluginId, config);
            this.savedRevision = savingRevision;
            this.showState(this.revision === this.savedRevision ? 'Saved successfully' : 'Saved. New edits are not saved yet.');
            await this.onSaved();
        } catch {
            this.showState('Changes not saved');
            error.textContent = 'Could not save configuration. Your edits are still here; retry when the server is available.';
        } finally {
            this.saving = false;
            this.saveButton.disabled = !this.loaded;
        }
    }
}
