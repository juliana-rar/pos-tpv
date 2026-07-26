// Client-side floor-plan editor for the Tables page — drives tables (.tbl), non-table decor
// elements (.decor: plants, walls, doors, bar counter, columns, windows) and named zone
// boundary boxes (.floor-zone).
// Blazor renders them; this module owns their geometry while editing (drag / resize /
// rotate / lock) so there are no per-pixel server round-trips. On save, Blazor pulls the final
// geometry via getLayout()/getDecorLayout()/getZoneLayout() and persists it.

let container = null;
let action = null;      // { el, mode, startX, startY, x, y, w, h, rot, cx, cy }
let zoom = 1;            // current .floor-canvas CSS scale (see Tables.razor's Zoom controls)
let borderX = 0, borderY = 0; // .floor's own border (the wall frame) — getBoundingClientRect()
                               // includes it, but .floor-canvas's origin sits inside it

const GRID = 10;        // snap step in px
const MIN = 50;         // minimum table size

// Table shapes, indexed by the TableShape enum value (Square=0, Round=1, Rectangular=2, Oval=3).
// Kept in sync with the CSS `.tbl--{name}` classes and the icons shown on the shape toggle button.
const SHAPES = ['square', 'round', 'rectangular', 'oval'];
const SHAPE_ICONS = ['◻', '○', '▭', '⬭'];

export function init(el, currentZoom) {
    container = el;
    zoom = currentZoom || 1;
    const cs = getComputedStyle(el);
    borderX = parseFloat(cs.borderLeftWidth) || 0;
    borderY = parseFloat(cs.borderTopWidth) || 0;
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

// Non-table floor elements (plants, walls, doors, bar counter, columns, windows) — same
// geometry shape as getLayout() above minus the table-only shape field, see FloorDecorLayoutDto.
export function getDecorLayout() {
    const out = [];
    if (!container) return out;
    container.querySelectorAll('.decor').forEach(d => {
        out.push({
            id: parseInt(d.dataset.id, 10),
            positionX: num(d.dataset.x),
            positionY: num(d.dataset.y),
            width: num(d.dataset.w),
            height: num(d.dataset.h),
            rotation: num(d.dataset.rot),
            isLocked: d.dataset.locked === 'true'
        });
    });
    return out;
}

// Named zone boundary boxes — no rotation/lock/shape, just position + size, see FloorZoneLayoutDto.
export function getZoneLayout() {
    const out = [];
    if (!container) return out;
    container.querySelectorAll('.floor-zone').forEach(z => {
        out.push({
            id: parseInt(z.dataset.id, 10),
            positionX: num(z.dataset.x),
            positionY: num(z.dataset.y),
            width: num(z.dataset.w),
            height: num(z.dataset.h)
        });
    });
    return out;
}

function onDown(e) {
    // Decor's own delete button (only rendered outside edit mode, like a zone's rename/delete)
    // is a plain Blazor round-trip — let the click reach it undisturbed instead of arming a
    // drag/pointer-capture on it. (Zone rename/delete never coexist with this listener: it's
    // only attached during _editing, and those buttons only render outside it.)
    if (e.target.closest('.decor__delete')) return;

    const el = e.target.closest('.tbl, .decor, .floor-zone');
    if (!el || !container.contains(el)) return;

    // Lock / unlock toggle is handled entirely here so Blazor never re-renders mid-edit.
    if (e.target.closest('.tbl__lock')) {
        const locked = el.dataset.locked === 'true';
        el.dataset.locked = (!locked).toString();
        el.classList.toggle(el.classList.contains('decor') ? 'decor--locked' : 'tbl--locked', !locked);
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

    // Joined tables move as one unit — on a drag (not resize/rotate, which stay per-table),
    // collect every table sharing this one's group id, itself included, and carry them all
    // by the same delta so the group never drifts apart on the floor plan.
    const groupId = el.dataset.group;
    const groupEls = (mode === 'drag' && groupId)
        ? Array.from(container.querySelectorAll('.tbl[data-group="' + groupId + '"]'))
            .map(g => ({ el: g, x: num(g.dataset.x), y: num(g.dataset.y) }))
        : [{ el, x, y }];
    // The "Joined" badge floats at the group's midpoint — drag it along too, live, instead of
    // leaving it stranded until the next server render (which only happens on Save layout).
    const badge = (mode === 'drag' && groupId) ? container.querySelector('.tbl__group[data-group="' + groupId + '"]') : null;

    action = {
        el, mode, groupEls, badge,
        startX: e.clientX, startY: e.clientY,
        x, y, w, h, rot: num(el.dataset.rot),
        // Table centre in viewport space (for rotation) — canvas-space x/y/w/h need scaling by
        // the current zoom factor, and rect.left/top (the outer .floor's border box) need the
        // wall-frame border subtracted, to land on .floor-canvas's own (unscaled) origin.
        cx: rect.left + borderX + (x + w / 2) * zoom,
        cy: rect.top + borderY + (y + h / 2) * zoom
    };
    groupEls.forEach(g => g.el.classList.add('tbl--active'));
    el.setPointerCapture?.(e.pointerId);
    e.preventDefault();
}

function onMove(e) {
    if (!action) return;
    const { el, mode, groupEls, badge } = action;
    // Mouse deltas are screen pixels; the canvas is scaled by `zoom`, so a screen pixel is
    // worth 1/zoom canvas pixels (zoomed in → each screen px is a smaller canvas move).
    const dx = (e.clientX - action.startX) / zoom;
    const dy = (e.clientY - action.startY) / zoom;

    if (mode === 'drag') {
        groupEls.forEach(g => {
            const nx = Math.max(0, snap(g.x + dx));
            const ny = Math.max(0, snap(g.y + dy));
            g.el.dataset.x = nx; g.el.dataset.y = ny;
            g.el.style.left = nx + 'px';
            g.el.style.top = ny + 'px';
        });
        if (badge) {
            // Matches GroupCenterStyle in Tables.razor: horizontal centre of the group, anchored
            // to its top edge (the CSS transform then floats the badge just above that point).
            const cx = groupEls.reduce((s, g) => s + num(g.el.dataset.x) + num(g.el.dataset.w) / 2, 0) / groupEls.length;
            const topY = Math.min(...groupEls.map(g => num(g.el.dataset.y)));
            badge.style.left = cx + 'px';
            badge.style.top = topY + 'px';
        }
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
        el.style.setProperty('--rot', ang + 'deg');   // keep the label upright (see .tbl__content)
    }
}

function onUp() {
    if (!action) return;
    action.groupEls.forEach(g => g.el.classList.remove('tbl--active'));
    action = null;
}

function snap(v) { return Math.round(v / GRID) * GRID; }
function num(v) { const n = parseFloat(v); return isNaN(n) ? 0 : n; }
