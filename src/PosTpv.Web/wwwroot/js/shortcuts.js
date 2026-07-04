// Forwards a curated set of keydown events to a .NET component. Typing in a field is ignored
// (except Escape), and OS/browser combos (Ctrl/Cmd/Alt) are left alone.
let handler = null;

const LETTERS = 'kptc';
const NAMED = ['Escape', 'Enter', 'Backspace', 'Delete', '?', '+', '-', '='];

export function register(ref) {
    unregister();
    handler = (e) => {
        const tag = (e.target.tagName || '').toLowerCase();
        const typing = tag === 'input' || tag === 'textarea' || tag === 'select' || e.target.isContentEditable;
        if (typing && e.key !== 'Escape') return;
        if (e.ctrlKey || e.metaKey || e.altKey) return;

        const k = e.key;
        const handled = NAMED.includes(k) || /^[1-9]$/.test(k) || (k.length === 1 && LETTERS.includes(k.toLowerCase()));
        if (!handled) return;

        e.preventDefault();
        ref.invokeMethodAsync('OnKey', k);
    };
    document.addEventListener('keydown', handler);
}

export function unregister() {
    if (handler) {
        document.removeEventListener('keydown', handler);
        handler = null;
    }
}
