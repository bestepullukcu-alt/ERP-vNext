'use strict';

/*
 * MOD-0022 E5W1 — the drawing area (pack §11.2; mockup v2 attachPad / redraw / paint / padPng, v2.app.js:136-175).
 *
 * ONE input path for mouse, pen and touch: Pointer Events with pointer capture (touch-action: none on the surface, so a finger
 * draws instead of scrolling). Strokes are kept NORMALIZED (0..1 of the surface), so a resize (or the full-screen landscape pad on a
 * 375 px phone) redraws the same signature, and they travel as Appearance.drawing.strokes (ESIGN-BUNDLE: points 0..1). The image
 * is a PNG rendered from the strokes at a fixed height (200 px), base64 WITHOUT the data: prefix (Platform reads raw base64).
 *
 *   const pad = ESignPad.attach(rootElement);   // [data-esign-pad]
 *   pad.isEmpty() · pad.strokes() · pad.undo() · pad.clear() · pad.png() · pad.setName(text) · pad.setPen({ width, color })
 *   pad.load(strokes) · pad.onChange(fn) · pad.redraw()
 */
(function (global) {
    const INK = { black: '#1b1b1f', navy: '#1f2a5a' };
    const WIDTH = { thin: 1.4, medium: 2.6 };

    function paint(ctx, strokes, w, h, pen) {
        ctx.strokeStyle = ctx.fillStyle = INK[pen.color] || INK.black;
        ctx.lineWidth = (WIDTH[pen.width] || WIDTH.medium) * Math.max(1, h / 150);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        strokes.forEach((s) => {
            if (!s.length) return;
            ctx.beginPath();
            if (s.length === 1) {
                ctx.arc(s[0][0] * w, s[0][1] * h, ctx.lineWidth / 2, 0, Math.PI * 2);
                ctx.fill();
                return;
            }
            ctx.moveTo(s[0][0] * w, s[0][1] * h);
            for (let i = 1; i < s.length - 1; i++) {
                ctx.quadraticCurveTo(s[i][0] * w, s[i][1] * h, (s[i][0] + s[i + 1][0]) / 2 * w, (s[i][1] + s[i + 1][1]) / 2 * h);
            }
            const last = s[s.length - 1];
            ctx.lineTo(last[0] * w, last[1] * h);
            ctx.stroke();
        });
    }

    function attach(root) {
        if (!root) return null;
        if (root.__esignPad) return root.__esignPad;
        const canvas = root.querySelector('[data-esign-pad-canvas]');
        const hint = root.querySelector('[data-esign-pad-hint]');
        const name = root.querySelector('[data-esign-pad-name]');
        const undoBtn = root.querySelector('[data-esign-pad-undo]');
        const clearBtn = root.querySelector('[data-esign-pad-clear]');
        let strokes = [];
        let current = null;
        let pen = { width: 'medium', color: 'black' };
        const listeners = [];

        const clamp = (v) => Math.min(1, Math.max(0, v));
        const pos = (e) => {
            const r = canvas.getBoundingClientRect();
            return [clamp((e.clientX - r.left) / (r.width || 1)), clamp((e.clientY - r.top) / (r.height || 1))];
        };

        function redraw() {
            const dpr = global.devicePixelRatio || 1;
            const w = canvas.clientWidth;
            const h = canvas.clientHeight;
            if (!w || !h) return;
            if (canvas.width !== Math.round(w * dpr) || canvas.height !== Math.round(h * dpr)) {
                canvas.width = Math.round(w * dpr);
                canvas.height = Math.round(h * dpr);
            }
            const ctx = canvas.getContext('2d');
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            ctx.clearRect(0, 0, w, h);
            paint(ctx, strokes, w, h, pen);
        }

        function changed() {
            const empty = strokes.length === 0;
            if (hint) hint.classList.toggle('d-none', !empty);
            if (undoBtn) undoBtn.disabled = empty;
            if (clearBtn) clearBtn.disabled = empty;
            listeners.forEach((fn) => fn(api));
        }

        canvas.addEventListener('pointerdown', (e) => {
            e.preventDefault();
            try { canvas.setPointerCapture(e.pointerId); } catch (_) { /* older browsers */ }
            current = [pos(e)];
            strokes.push(current);
            redraw();
        });
        canvas.addEventListener('pointermove', (e) => {
            if (!current) return;
            current.push(pos(e));
            redraw();
        });
        const end = () => {
            if (!current) return;
            current = null;
            changed();
        };
        canvas.addEventListener('pointerup', end);
        canvas.addEventListener('pointercancel', end);
        if (undoBtn) undoBtn.addEventListener('click', () => api.undo());
        if (clearBtn) clearBtn.addEventListener('click', () => api.clear());
        if (global.ResizeObserver) new global.ResizeObserver(() => redraw()).observe(canvas);

        const api = {
            isEmpty: () => strokes.length === 0,
            strokes: () => strokes.map((s) => s.map((p) => [Math.round(p[0] * 10000) / 10000, Math.round(p[1] * 10000) / 10000])),
            load: (next) => { strokes = (next || []).map((s) => s.map((p) => [p[0], p[1]])); redraw(); changed(); },
            undo: () => { strokes.pop(); redraw(); changed(); },
            clear: () => { strokes = []; current = null; redraw(); changed(); },
            setName: (text) => { if (name) name.textContent = text ? `× ${text}` : ''; },
            setPen: (next) => { pen = Object.assign({}, pen, next || {}); redraw(); },
            pen: () => Object.assign({}, pen),
            onChange: (fn) => listeners.push(fn),
            redraw,
            /** PNG of the strokes, fixed height 200 px, the surface's aspect; raw base64 (no data: prefix). */
            png: () => {
                const aspect = canvas.clientHeight ? canvas.clientWidth / canvas.clientHeight : 3;
                const H = 200;
                const W = Math.min(2000, Math.max(200, Math.round(H * aspect)));
                const out = global.document.createElement('canvas');
                out.width = W;
                out.height = H;
                paint(out.getContext('2d'), strokes, W, H, pen);
                return out.toDataURL('image/png').split(',')[1];
            }
        };
        root.__esignPad = api;
        changed();
        global.requestAnimationFrame ? global.requestAnimationFrame(redraw) : redraw();
        return api;
    }

    global.ESignPad = { attach, INK: Object.freeze(Object.assign({}, INK)) };
})(window);
