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
