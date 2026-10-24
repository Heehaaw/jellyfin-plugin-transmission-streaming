import TooltipDisplay from './ConfigurationPage?name=transmission-streaming-tooltip-display.js';
import ConfigurationForm from './ConfigurationPage?name=transmission-streaming-configuration.js';
import PathMappings from './ConfigurationPage?name=transmission-streaming-mappings.js';
import Activity from './ConfigurationPage?name=transmission-streaming-activity.js';
import {applyFieldHelp} from './ConfigurationPage?name=transmission-streaming-tooltips.js';

// Jellyfin constructs the default export with its page element after loading this native ES module.
export default class SettingsPage {
    constructor(page) {
        this.lifecycle = {active: false, generation: 0};
        this.activity = new Activity(page, this.lifecycle);
        const mappings = new PathMappings(page, this.lifecycle, () => this.configuration.markDirty());
        this.configuration = new ConfigurationForm(page, this.lifecycle, mappings, () => this.activity.refresh());
        applyFieldHelp(page);
        this.tooltips = new TooltipDisplay(page);
        ['pageshow', 'viewshow'].forEach(name => page.addEventListener(name, () => this.activate()));
        ['pagehide', 'viewhide', 'viewdestroy'].forEach(name => page.addEventListener(name, () => this.deactivate()));
    }

    async activate() {
        if (this.lifecycle.active) return;
        this.lifecycle.active = true;
        const generation = ++this.lifecycle.generation;
        void this.activity.refresh();
        if (!this.configuration.loaded) await this.configuration.load(generation);
        else this.configuration.showState();
    }

    deactivate() {
        this.lifecycle.active = false;
        this.lifecycle.generation++;
        this.activity.stop();
        this.tooltips.hide();
    }
}
