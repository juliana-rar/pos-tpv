// Small client-side helpers. The app is Blazor Server, so this stays intentionally tiny.
window.posTheme = {
    get() {
        return document.documentElement.getAttribute('data-theme') || 'light';
    },
    set(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('pos-theme', theme);
        return theme;
    },
    toggle() {
        const next = this.get() === 'dark' ? 'light' : 'dark';
        return this.set(next);
    }
};

// Blazor's enhanced navigation re-renders <html> from the server's markup on every internal
// link click, which wipes the data-theme attribute the inline <head> script only sets on a
// real full page load. Re-apply the persisted value after each enhanced navigation so dark
// mode survives clicking around the app.
function reapplyPersistedUi() {
    window.posTheme.set(localStorage.getItem('pos-theme') || 'light');
}
if (window.Blazor?.addEventListener) {
    window.Blazor.addEventListener('enhancedload', reapplyPersistedUi);
}

// Keep the order panel scrolled to its newest line as items are added.
window.posScrollBottom = function (el) {
    if (el) el.scrollTop = el.scrollHeight;
};

// Same purpose as posScrollBottom above, but for the comanda editor (OrderEditor.razor), where a
// newly added line can land inside a course's own inner scroll (.order__group-lines--scroll) as
// well as the outer order panel — a flat scrollTop-to-bottom wouldn't reach into that nested
// container. scrollIntoView walks every scrollable ancestor on its own, so it works for both.
window.posScrollIntoView = function (id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
};

// Drag-to-resize the order panel in the comanda editor. `containerEl` is the grid whose
// second column width the handle controls (via the --order-w custom property); min/max keep
// it from shrinking past where a line's controls would wrap, or growing past being useful.
window.posOrderResize = function (handleEl, containerEl, min, max) {
    if (!handleEl || !containerEl || handleEl.dataset.resizeBound) return;
    handleEl.dataset.resizeBound = '1';

    function onMove(e) {
        const rect = containerEl.getBoundingClientRect();
        const w = Math.min(max, Math.max(min, rect.right - e.clientX));
        containerEl.style.setProperty('--order-w', w + 'px');
    }
    function onUp() {
        document.body.style.userSelect = '';
        document.removeEventListener('pointermove', onMove);
        document.removeEventListener('pointerup', onUp);
    }
    handleEl.addEventListener('pointerdown', function (e) {
        e.preventDefault();
        document.body.style.userSelect = 'none';
        document.addEventListener('pointermove', onMove);
        document.addEventListener('pointerup', onUp);
    });
};

// Keeps each order-time group's time label pinned to the vertically-centered line of its own
// group while that group is scrolled through, instead of scrolling off with the rest of the
// text — approximates CSS position:sticky for groups shorter than the scroll viewport (where a
// sticky offset would never engage). Re-run on every scroll of any scrolling ancestor inside
// rootEl and after every render (call this from OnAfterRenderAsync) since adding/removing lines
// changes group heights.
window.posStickyTimeLabels = function (rootEl) {
    if (!rootEl) return;

    function visibleBounds(groupEl) {
        let top = -Infinity, bottom = Infinity;
        for (let node = groupEl.parentElement; node && node !== rootEl.parentElement; node = node.parentElement) {
            const overflowY = getComputedStyle(node).overflowY;
            if (overflowY === 'auto' || overflowY === 'scroll') {
                const r = node.getBoundingClientRect();
                top = Math.max(top, r.top);
                bottom = Math.min(bottom, r.bottom);
            }
        }
        return { top, bottom };
    }

    function updateGroup(groupEl) {
        const pin = groupEl.querySelector('.order__time-group-time-pin');
        if (!pin) return;
        const gRect = groupEl.getBoundingClientRect();
        const bounds = visibleBounds(groupEl);
        const visTop = Math.max(gRect.top, bounds.top);
        const visBottom = Math.min(gRect.bottom, bounds.bottom);
        if (visBottom <= visTop) return; // fully scrolled out of view — leave it be
        const visCenter = (visTop + visBottom) / 2;
        // Clamped to the group's own height so the label never drifts past its first/last line.
        const half = gRect.height / 2 - pin.offsetHeight / 2;
        let offset = visCenter - (gRect.top + gRect.height / 2);
        offset = half > 0 ? Math.max(-half, Math.min(half, offset)) : 0;
        pin.style.transform = offset ? `translateY(${offset}px)` : '';
    }

    function updateAll() {
        rootEl.querySelectorAll('.order__time-group').forEach(updateGroup);
    }

    updateAll();
    if (!rootEl.dataset.stickyBound) {
        rootEl.dataset.stickyBound = '1';
        // scroll doesn't bubble, but a capture-phase listener on an ancestor still sees it fire
        // on the way down — catches both the outer panel and any nested course scroll this way.
        rootEl.addEventListener('scroll', updateAll, true);
        window.addEventListener('resize', updateAll);
    }
};

// Opens the native picker for a date input. Used by the custom calendar icon next to the
// reservations day input — its own ::-webkit-calendar-picker-indicator reserves a fixed gap
// that CSS can't tighten, so that icon is hidden and this one drives the same input instead.
window.posShowDatePicker = function (el) {
    el?.showPicker?.();
};

// Play a short chime when the kitchen marks an order ready (best-effort; ignored if blocked).
window.posBeep = function () {
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain); gain.connect(ctx.destination);
        osc.type = 'sine'; osc.frequency.value = 880;
        gain.gain.setValueAtTime(0.15, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.4);
        osc.start(); osc.stop(ctx.currentTime + 0.4);
    } catch (e) { /* audio not available */ }
};
