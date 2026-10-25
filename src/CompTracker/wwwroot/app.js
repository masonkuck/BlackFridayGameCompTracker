// Small client-side helpers for the Blazor UI. Loaded once from App.razor.

window.compTracker = {
    // Focus the first field in a container (used when a modal opens).
    focusFirst(el) {
        if (!el) return;
        const target = el.querySelector('input:not([type=hidden]), select, textarea') ?? el.querySelector('button');
        target?.focus();
        target?.select?.();
    },

    // Download a file without leaving the page.
    download(url) {
        const a = document.createElement('a');
        a.href = url;
        a.download = '';
        document.body.append(a);
        a.click();
        a.remove();
    },

    // Briefly glow a dashboard game card when a score is posted in it.
    flashGame(gameId) {
        const card = document.querySelector(`.game-card[data-game-id="${gameId}"]`);
        if (!card) return;
        card.classList.remove('flash');
        void card.offsetWidth; // restart the animation if it's already running
        card.classList.add('flash');
        card.addEventListener('animationend', () => card.classList.remove('flash'), { once: true });
    },
};

// ---- Dashboard sound effects (files in wwwroot/sounds, made by tools/make_sounds.py) ----
// Browsers block audio until someone clicks or presses a key on the page, so sound is opt-in with the
// speaker button and the choice is remembered per browser. After a reload any click re-arms it.
// The state goes on <html data-sound="off|on|blocked"> (outside Blazor, so re-renders don't reset it).

window.compTracker.sound = (() => {
    const storageKey = 'compTracker.sound';
    const names = ['ping', 'crown', 'champion'];
    const buffers = {};
    let ctx = null;
    let enabled = false;
    try { enabled = localStorage.getItem(storageKey) === 'on'; } catch { }

    function context() {
        if (!ctx) {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return null;
            ctx = new AudioCtx();
            ctx.onstatechange = render;
        }
        return ctx;
    }

    function load(name) {
        buffers[name] ??= fetch(`sounds/${name}.wav`)
            .then((r) => r.arrayBuffer())
            .then((data) => context().decodeAudioData(data));
        return buffers[name];
    }

    function render() {
        const running = ctx?.state === 'running';
        document.documentElement.dataset.sound = !enabled ? 'off' : running ? 'on' : 'blocked';
    }

    // Only call from a click/key handler (or when the browser allows autoplay); otherwise resume() waits.
    async function unlock() {
        const c = context();
        if (!c) return;
        if (c.state !== 'running') {
            try { await c.resume(); } catch { }
        }
        names.forEach(load);
        render();
    }

    async function play(name) {
        if (!enabled || ctx?.state !== 'running') {
            render();
            return;
        }
        const source = ctx.createBufferSource();
        source.buffer = await load(name);
        const gain = ctx.createGain();
        gain.gain.value = 0.8;
        source.connect(gain).connect(ctx.destination);
        source.start();
    }

    async function setEnabled(on) {
        enabled = on;
        try { localStorage.setItem(storageKey, on ? 'on' : 'off'); } catch { }
        if (on) {
            await unlock();
            play('ping'); // so you can hear that it worked
        }
        render();
    }

    const isToggle = (e) => e.target.closest?.('[data-sound-toggle]');
    const armOnInteraction = (e) => { if (enabled && !isToggle(e)) unlock(); };
    document.addEventListener('pointerdown', armOnInteraction, { capture: true });
    document.addEventListener('keydown', armOnInteraction, { capture: true });
    document.addEventListener('click', (e) => {
        if (!isToggle(e)) return;
        // While blocked, the button arms the sound rather than turning it off.
        setEnabled(!(enabled && ctx?.state === 'running'));
    });

    // Launched with --autoplay-policy=no-user-gesture-required (kiosk), the context starts running on its own.
    if (enabled) {
        context();
        if (ctx?.state === 'running') names.forEach(load);
    }
    render();

    return { play, setEnabled };
})();

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
