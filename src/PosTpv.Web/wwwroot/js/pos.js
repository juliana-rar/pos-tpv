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
