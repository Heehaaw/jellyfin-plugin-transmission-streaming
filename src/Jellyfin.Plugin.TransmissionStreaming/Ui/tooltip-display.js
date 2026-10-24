const displayDelay = 150;
const viewportMargin = 16;
const anchorGap = 8;

export default class TooltipDisplay {
    constructor(page) {
        this.page = page;
        this.bubble = document.createElement('div');
        this.bubble.className = 'ts-tooltip';
        this.bubble.setAttribute('role', 'tooltip');
        this.bubble.hidden = true;
        page.append(this.bubble);

        // Delegate to include mapping rows created after configuration or discovery loads.
        page.addEventListener('pointerover', event => this.schedule(event.target));
        page.addEventListener('focusin', event => this.schedule(event.target));
        for (const name of ['pointerout', 'focusout']) {
            page.addEventListener(name, event => {
                if (!this.anchor?.contains(event.relatedTarget)) this.hide();
            });
        }
        for (const name of ['click', 'input', 'keydown', 'scroll']) {
            page.addEventListener(name, () => this.hide(), true);
        }
    }

    schedule(target) {
        const anchor = target.closest('[data-ts-tooltip]');
        if (anchor === this.anchor) return;
        this.hide();
        if (!anchor || !this.page.contains(anchor) || anchor.closest(':disabled')) return;
        this.anchor = anchor;
        this.timer = setTimeout(() => this.show(), displayDelay);
    }

    show() {
        if (!this.anchor?.isConnected) return;
        const bounds = this.anchor.getBoundingClientRect();
        this.bubble.textContent = this.anchor.dataset.tsTooltip;
        this.bubble.hidden = false;
        const width = this.bubble.offsetWidth;
        const height = this.bubble.offsetHeight;
        const left = Math.max(viewportMargin, Math.min(bounds.left, innerWidth - width - viewportMargin));
        const below = bounds.bottom + anchorGap;
        const top = below + height <= innerHeight - viewportMargin ? below : Math.max(viewportMargin, bounds.top - height - anchorGap);
        this.bubble.style.left = left + 'px';
        this.bubble.style.top = top + 'px';
    }

    hide() {
        clearTimeout(this.timer);
        this.anchor = null;
        this.bubble.hidden = true;
    }
}
