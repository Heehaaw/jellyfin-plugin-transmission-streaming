const fields = {
    Enabled: 'Adds playable entries for arr-managed downloads. Save to apply.',
    TransmissionUrl: 'Transmission RPC address reachable from Jellyfin, usually ending in /transmission/rpc.',
    TransmissionUsername: 'Username configured for Transmission RPC authentication.',
    TransmissionPassword: 'Password configured for Transmission RPC authentication.',
    RadarrUrl: 'Radarr address reachable from Jellyfin. Leave blank when unused.',
    RadarrApiKey: 'Radarr API key from Settings → General.',
    SonarrUrl: 'Sonarr address reachable from Jellyfin. Leave blank when unused.',
    SonarrApiKey: 'Sonarr API key from Settings → General.',
    ProjectionRoot: 'Jellyfin creates streaming entries here. Keep this separate from downloaded and imported media.',
    JellyfinPublicUrl: 'Every player and the Jellyfin server must reach this address, including any reverse-proxy base path.',
    RemoteMediaRoot: 'Fallback translation used only for a service without its own mapping rows.',
    LocalMediaRoot: 'Jellyfin’s mount of the shared fallback root.',
    PollSeconds: 'Background scan interval. Activity refreshes separately every 10 seconds.',
    PieceWaitSeconds: 'Playback fails after this many seconds without new bytes. Receiving bytes resets the timer.',
    PrioritizePlayback: 'Enables sequential mode and moves Transmission’s cursor toward missing playback bytes.',
    ShowDownloadStatus: 'Adds progress and status to streaming entry titles. Live activity always shows the current status.'
};

const roles = {
    'Default downloads': 'Transmission’s default completed-download directory.',
    'Incomplete downloads': 'Transmission’s incomplete-download directory while downloads are active.',
    'Custom download folder': 'A directory used by an active torrent instead of the default download folder.',
    'Mapped folder': 'A folder translation. Discover folders to identify its role.',
    'Movie library': 'Folder used for Radarr movie imports.',
    'Series library': 'Folder used for Sonarr episode imports.'
};

export const explanations = {
    discover: 'Checks folders with the connection details currently in this form. Existing edits are kept.',
    add: 'Adds an editable translation. Nothing changes until you save.',
    remove: 'Removes only this translation. No files are moved or deleted.',
    localPath: 'Jellyfin’s absolute path to the same folder. Verify the mount and permissions.',
    missingPath: 'Enter Jellyfin’s absolute path to this folder. A suggestion does not confirm readability.',
    refresh: 'Fetches current scan and playback status.',
    save: 'Saves connection details, folder translations and playback settings.'
};

export function help(element, explanation) {
    if (!element || !explanation) return;
    element.classList.toggle('ts-help', !element.closest('button'));
    element.dataset.tsTooltip = explanation;
    element.removeAttribute('title');
    element.setAttribute('aria-description', explanation);
}

export function roleHelp(element, role) {
    help(element, roles[role]);
}

export function applyFieldHelp(page) {
    for (const [name, explanation] of Object.entries(fields)) {
        const input = page.querySelector('#' + name);
        const label = page.querySelector('label[for="' + name + '"]') ?? page.querySelector('[data-help-for="' + name + '"]');
        help(label, explanation);
        input?.setAttribute('aria-description', explanation);
        if (input) input.dataset.tsTooltip = explanation;
    }
    const metrics = [
        ['streamingStatus', 'Whether saved settings enable the background download scan.'],
        ['lastSuccessfulPoll', 'Most recent background download scan that finished without an error.'],
        ['streamingItemCount', 'Persistent streaming entries currently tracked by the plugin.'],
        ['streamingReaderCount', 'Open playback copy operations. One player can make several requests.']
    ];
    for (const [name, explanation] of metrics) help(page.querySelector('#' + name)?.parentElement.querySelector('dt'), explanation);
}
