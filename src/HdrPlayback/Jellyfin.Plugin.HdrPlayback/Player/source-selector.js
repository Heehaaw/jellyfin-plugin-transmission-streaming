(function () {
    'use strict';
    if (window.JellyfinHdrSourceSelector) return;
    window.JellyfinHdrSourceSelector = true;

    const prefix = 'hdr-conversion:';
    const marker = 'hdrConversionChoice';

    function refresh(form) {
        const sources = form.querySelector('.selectSource');
        const video = form.querySelector('.selectVideo');
        if (!sources || !video) return;
        const originalId = sources.value.startsWith(prefix) ? sources.value.slice(prefix.length) : sources.value;
        const convertedId = prefix + originalId;
        const sourceOptions = Array.from(sources.options);
        if (!sourceOptions.some(option => option.value === convertedId)) return;
        const originalTracks = Array.from(video.options).filter(option => !option.dataset[marker]);
        if (originalTracks.length !== 1) return;
        let converted = Array.from(video.options).find(option => option.dataset[marker]);
        if (!converted) {
            converted = document.createElement('option');
            converted.dataset[marker] = 'true';
            video.add(converted);
        }
        converted.value = convertedId;
        if (converted.textContent !== 'HDR (converted)') converted.textContent = 'HDR (converted)';
        video.removeAttribute('disabled');
        video.value = sources.value.startsWith(prefix) ? convertedId : originalTracks[0].value;

        // Conversion is chosen under Video. Keep the native source option available for playback routing.
        sourceOptions.filter(option => option.value.startsWith(prefix)).forEach(option => { option.hidden = true; });
        const versionContainer = form.querySelector('.selectSourceContainer');
        if (versionContainer) versionContainer.classList.toggle('hide', sourceOptions.filter(option => !option.value.startsWith(prefix)).length < 2);
    }

    document.addEventListener('change', function (event) {
        const video = event.target;
        if (!video.classList.contains('selectVideo')) return;
        const form = video.closest('.trackSelections');
        const sources = form && form.querySelector('.selectSource');
        if (!sources || !Array.from(video.options).some(option => option.dataset[marker])) return;
        const originalId = sources.value.startsWith(prefix) ? sources.value.slice(prefix.length) : sources.value;
        const selectedId = video.value.startsWith(prefix) ? video.value : originalId;
        if (sources.value === selectedId) return;
        const audio = form.querySelector('.selectAudio');
        const subtitles = form.querySelector('.selectSubtitles');
        const audioIndex = audio && audio.value;
        const subtitleIndex = subtitles && subtitles.value;
        sources.value = selectedId;
        sources.dispatchEvent(new Event('change', { bubbles: true }));
        // Native source changes rebuild the track lists. Both choices use the same file, so retain explicit track selections.
        if (audio && Array.from(audio.options).some(option => option.value === audioIndex)) audio.value = audioIndex;
        if (subtitles && Array.from(subtitles.options).some(option => option.value === subtitleIndex)) subtitles.value = subtitleIndex;
        refresh(form);
    });

    let scheduled = false;
    function scheduleRefresh() {
        if (scheduled) return;
        scheduled = true;
        queueMicrotask(function () {
            scheduled = false;
            document.querySelectorAll('.trackSelections').forEach(refresh);
        });
    }
    // Jellyfin replaces detail views and their options during navigation and version changes.
    new MutationObserver(scheduleRefresh).observe(document.documentElement, { childList: true, subtree: true });
    scheduleRefresh();
})();
