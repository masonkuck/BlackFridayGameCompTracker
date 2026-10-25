// Small client-side helpers for the Blazor UI. Loaded once from App.razor.

window.compTracker = {
    // Focus the first field in a container (used when a modal opens).
    focusFirst(el) {
        if (!el) return;
        const target = el.querySelector('input:not([type=hidden]), select, textarea') ?? el.querySelector('button');
        target?.focus();
        target?.select?.();
    },
};

// ---- Time inputs (Components/TimeInput.razor) ----
// Boxes are marked data-time-part="h|m|s|f" inside a .time-input group.

const timeBox = (target) => target?.closest?.('[data-time-part]');
const boxesOf = (box) => [...box.closest('.time-input').querySelectorAll('[data-time-part]')];
const nextBox = (box) => { const all = boxesOf(box); return all[all.indexOf(box) + 1]; };
const prevBox = (box) => { const all = boxesOf(box); return all[all.indexOf(box) - 1]; };

function setBox(box, value) {
    if (!box) return;
    box.value = value;
    // Let Blazor's @bind (oninput) see the change.
    box.dispatchEvent(new Event('input', { bubbles: true }));
}

document.addEventListener('keydown', (e) => {
    const box = timeBox(e.target);
    if (!box) return;

    if (e.key === ':' || e.key === '.') {
        // Typing a separator jumps to the next box, like typing the time out normally.
        e.preventDefault();
        const next = nextBox(box);
        next?.focus();
        next?.select();
    } else if (e.key === 'Backspace' && box.value === '' && prevBox(box)) {
        e.preventDefault();
        prevBox(box).focus();
    } else if (e.key.length === 1 && !/[0-9]/.test(e.key) && !e.ctrlKey && !e.metaKey) {
        e.preventDefault(); // digits only
    }
});

document.addEventListener('input', (e) => {
    const box = timeBox(e.target);
    if (!box || !e.isTrusted) return;
    const max = Number(box.dataset.maxDigits || 0);
    if (max && box.value.length >= max) {
        const next = nextBox(box);
        next?.focus();
        next?.select();
    }
});

// Pasting a full time like "1:02:03.5" or "4:32.150" into any box fills all of them.
document.addEventListener('paste', (e) => {
    const box = timeBox(e.target);
    if (!box) return;
    const text = (e.clipboardData?.getData('text') ?? '').trim();
    const match = text.match(/^(?:(\d+):)?(?:(\d+):)?(\d+)(?:\.(\d{1,3}))?$/);
    if (!match || !/[:.]/.test(text)) return;
    e.preventDefault();

    // Colon-separated parts fill from the right: sec, then min, then hours.
    const colonParts = text.split('.')[0].split(':');
    const sec = colonParts.pop() ?? '';
    const min = colonParts.pop() ?? '';
    const hr = colonParts.pop() ?? '';
    const fraction = match[4] ?? '';

    const group = box.closest('.time-input');
    const part = (name) => group.querySelector(`[data-time-part="${name}"]`);
    if (hr && !part('h')) {
        // No hours box showing: fold hours into minutes.
        setBox(part('m'), String(Number(hr) * 60 + Number(min || 0)));
    } else {
        setBox(part('h'), hr);
        setBox(part('m'), min);
    }
    setBox(part('s'), sec.padStart(2, '0'));
    setBox(part('f'), fraction);
    part('f')?.focus();
});
