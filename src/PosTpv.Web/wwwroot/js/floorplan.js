// Client-side floor-plan editor for the Tables page.
// Blazor renders the tables; this module owns their geometry while editing (drag / resize /
// rotate / lock) so there are no per-pixel server round-trips. On save, Blazor pulls the final
// geometry via getLayout() and persists it.

let container = null;
let action = null;      // { el, mode, startX, startY, x, y, w, h, rot, cx, cy }

const GRID = 10;        // snap step in px
const MIN = 50;         // minimum table size

// Table shapes, indexed by the TableShape enum value (Square=0, Round=1, Rectangular=2, Oval=3).
// Kept in sync with the CSS `.tbl--{name}` classes and the icons shown on the shape toggle button.
const SHAPES = ['square', 'round', 'rectangular', 'oval'];
const SHAPE_ICONS = ['◻', '○', '▭', '⬭'];

export function init(el) {
    container = el;
    container.addEventListener('pointerdown', onDown);
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
}

export function dispose() {
    if (!container) return;
    container.removeEventListener('pointerdown', onDown);
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
    container = null;
    action = null;
}

export function getLayout() {
    const out = [];
    if (!container) return out;
    container.querySelectorAll('.tbl').forEach(t => {
        out.push({
            id: parseInt(t.dataset.id, 10),
            positionX: num(t.dataset.x),
            positionY: num(t.dataset.y),
            width: num(t.dataset.w),
            height: num(t.dataset.h),
            rotation: num(t.dataset.rot),
            isLocked: t.dataset.locked === 'true',
            shape: num(t.dataset.shape)
        });
    });
    return out;
}

function onDown(e) {
    const el = e.target.closest('.tbl');
    if (!el || !container.contains(el)) return;

    // Lock / unlock toggle is handled entirely here so Blazor never re-renders mid-edit.
    if (e.target.closest('.tbl__lock')) {
        const locked = el.dataset.locked === 'true';
        el.dataset.locked = (!locked).toString();
        el.classList.toggle('tbl--locked', !locked);
        e.preventDefault();
        return;
    }

    // Shape toggle: cycle Square → Round → Rectangular → Oval. Handled here (like the lock)
    // so the change is instant and Blazor stays out of the way; it persists via getLayout().
    const shapeBtn = e.target.closest('.tbl__shape');
    if (shapeBtn) {
        const current = num(el.dataset.shape);
        const next = (current + 1) % SHAPES.length;
        el.classList.remove('tbl--' + SHAPES[current]);
        el.classList.add('tbl--' + SHAPES[next]);
        el.dataset.shape = next;
        shapeBtn.textContent = SHAPE_ICONS[next];
        e.preventDefault();
        return;
    }

    if (el.dataset.locked === 'true') return;

    const handle = e.target.closest('.tbl__handle');
    const mode = handle
        ? (handle.classList.contains('tbl__handle--rotate') ? 'rotate' : 'resize')
        : 'drag';

    const rect = container.getBoundingClientRect();
    const x = num(el.dataset.x), y = num(el.dataset.y), w = num(el.dataset.w), h = num(el.dataset.h);

    action = {
        el, mode,
        startX: e.clientX, startY: e.clientY,
        x, y, w, h, rot: num(el.dataset.rot),
        cx: rect.left + x + w / 2,     // table centre in viewport space (for rotation)
        cy: rect.top + y + h / 2
    };
    el.classList.add('tbl--active');
    el.setPointerCapture?.(e.pointerId);
    e.preventDefault();
}

function onMove(e) {
    if (!action) return;
    const { el, mode } = action;
    const dx = e.clientX - action.startX;
    const dy = e.clientY - action.startY;

    if (mode === 'drag') {
        const nx = Math.max(0, snap(action.x + dx));
        const ny = Math.max(0, snap(action.y + dy));
        el.dataset.x = nx; el.dataset.y = ny;
        el.style.left = nx + 'px';
        el.style.top = ny + 'px';
    } else if (mode === 'resize') {
        const nw = Math.max(MIN, snap(action.w + dx));
        const nh = Math.max(MIN, snap(action.h + dy));
        el.dataset.w = nw; el.dataset.h = nh;
        el.style.width = nw + 'px';
        el.style.height = nh + 'px';
    } else if (mode === 'rotate') {
        let ang = Math.atan2(e.clientY - action.cy, e.clientX - action.cx) * 180 / Math.PI + 90;
        ang = Math.round(ang / 5) * 5;
        if (ang < 0) ang += 360;
        el.dataset.rot = ang;
        el.style.transform = 'rotate(' + ang + 'deg)';
    }
}

function onUp() {
    if (!action) return;
    action.el.classList.remove('tbl--active');
    action = null;
}

function snap(v) { return Math.round(v / GRID) * GRID; }
function num(v) { const n = parseFloat(v); return isNaN(n) ? 0 : n; }
