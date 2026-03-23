window.initBallTrail = function () {
    if (window._ballTrailFrame) cancelAnimationFrame(window._ballTrailFrame);

    const canvas = document.getElementById('ball-trail');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    function resize() {
        canvas.width  = window.innerWidth;
        canvas.height = window.innerHeight;
    }
    resize();
    const onResize = () => resize();
    window.addEventListener('resize', onResize);

    const trail = [];
    const TRAIL_MS = 380; // how long the trail persists in milliseconds

    function frame() {
        // auto-stop if canvas was removed (Blazor navigation away from home)
        if (!document.getElementById('ball-trail')) {
            window._ballTrailFrame = null;
            window.removeEventListener('resize', onResize);
            return;
        }

        const now = performance.now();
        const ball = document.querySelector('.neon-ball');
        if (ball) {
            const r  = ball.getBoundingClientRect();
            const cx = r.left + r.width  / 2;
            const cy = r.top  + r.height / 2;
            trail.push({ x: cx, y: cy, t: now });
        }

        // Drop points older than TRAIL_MS
        while (trail.length > 0 && now - trail[0].t > TRAIL_MS) trail.shift();

        ctx.clearRect(0, 0, canvas.width, canvas.height);

        const n = trail.length;
        if (n >= 2) {
            const tail = trail[0];
            const head = trail[n - 1];

            // Single soft outer glow — ball-width, fades toward tail
            const gOuter = ctx.createLinearGradient(tail.x, tail.y, head.x, head.y);
            gOuter.addColorStop(0,   'rgba(0, 204, 255, 0)');
            gOuter.addColorStop(0.4, 'rgba(0, 204, 255, 0.08)');
            gOuter.addColorStop(1,   'rgba(0, 204, 255, 0.22)');
            ctx.beginPath();
            ctx.moveTo(trail[0].x, trail[0].y);
            for (let i = 1; i < n; i++) ctx.lineTo(trail[i].x, trail[i].y);
            ctx.strokeStyle = gOuter;
            ctx.lineWidth   = 70;
            ctx.lineCap     = 'round';
            ctx.lineJoin    = 'round';
            ctx.stroke();
        }

        window._ballTrailFrame = requestAnimationFrame(frame);
    }

    frame();

    window._ballTrailStop = function () {
        cancelAnimationFrame(window._ballTrailFrame);
        window._ballTrailFrame = null;
        window.removeEventListener('resize', onResize);
        if (canvas) ctx.clearRect(0, 0, canvas.width, canvas.height);
    };
};

window.stopBallTrail = function () {
    if (window._ballTrailStop) {
        window._ballTrailStop();
        window._ballTrailStop = null;
    }
};

// Re-initialize on Blazor enhanced navigation
(function () {
    function onEnhancedLoad() {
        if (typeof window.stopBallTrail === 'function') window.stopBallTrail();
        if (document.getElementById('ball-trail') && typeof window.initBallTrail === 'function') {
            window.initBallTrail();
        }
    }
    if (window.Blazor && typeof Blazor.addEventListener === 'function') {
        Blazor.addEventListener('enhancedload', onEnhancedLoad);
    } else {
        document.addEventListener('DOMContentLoaded', function () {
            if (window.Blazor && typeof Blazor.addEventListener === 'function') {
                Blazor.addEventListener('enhancedload', onEnhancedLoad);
            }
        });
    }
})();

window.initSubmissionsOrbs = function () {
    if (typeof window.stopSubmissionsOrbs === 'function') {
        window.stopSubmissionsOrbs();
    }

    const root = document.documentElement;
    const body = document.body;
    if (!root || !body) return;

    body.classList.add('submissions-orbs-active');

    const setVar = (name, value) => root.style.setProperty(name, value);

    const update = function () {
        const scrollY = window.scrollY || 0;
        const vw = window.innerWidth || 1280;
        const vh = window.innerHeight || 720;

        const s1 = Math.max(760, Math.min(1120, vw * 0.68));
        const s2 = Math.max(700, Math.min(1040, vw * 0.62));
        const s3 = Math.max(740, Math.min(1080, vw * 0.66));

        // Start points requested by user:
        // orb-1: top-left, orb-2: middle-bottom, orb-3: top-right.
        const start1x = -s1 * 0.42;
        const start1y = -s1 * 0.42;

        const start2x = vw * 0.5 - s2 * 0.5;
        const start2y = vh - s2 * 0.32;

        const start3x = vw - s3 * 0.58;
        const start3y = -s3 * 0.4;

        // All orbs travel toward the same center point, then continue past it
        // on the same straight trajectory as scrolling continues.
        const centerX = vw * 0.5;
        const centerY = vh * 0.52;
        const target1x = centerX - s1 * 0.5;
        const target1y = centerY - s1 * 0.5;
        const target2x = centerX - s2 * 0.5;
        const target2y = centerY - s2 * 0.5;
        const target3x = centerX - s3 * 0.5;
        const target3y = centerY - s3 * 0.5;

        // 0: static at start, 1: meet near center, >1: pass each other.
        const progress = Math.max(0, Math.min(1.9, scrollY / Math.max(420, vh * 1.1)));
        const lerp = (a, b) => a + (b - a) * progress;

        const x1 = lerp(start1x, target1x);
        const y1 = lerp(start1y, target1y);
        const x2 = lerp(start2x, target2x);
        const y2 = lerp(start2y, target2y);
        const x3 = lerp(start3x, target3x);
        const y3 = lerp(start3y, target3y);

        setVar('--sub-orb-1-size', `${s1}px`);
        setVar('--sub-orb-2-size', `${s2}px`);
        setVar('--sub-orb-3-size', `${s3}px`);

        setVar('--sub-orb-1-x', `${x1}px`);
        setVar('--sub-orb-1-y', `${y1}px`);
        setVar('--sub-orb-2-x', `${x2}px`);
        setVar('--sub-orb-2-y', `${y2}px`);
        setVar('--sub-orb-3-x', `${x3}px`);
        setVar('--sub-orb-3-y', `${y3}px`);
    };

    let ticking = false;
    const requestTick = function () {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(function () {
            update();
            ticking = false;
        });
    };

    const onScroll = function () { requestTick(); };
    const onResize = function () { requestTick(); };

    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onResize);
    update();

    window._submissionsOrbsCleanup = function () {
        window.removeEventListener('scroll', onScroll);
        window.removeEventListener('resize', onResize);
        body.classList.remove('submissions-orbs-active');

        root.style.removeProperty('--sub-orb-1-size');
        root.style.removeProperty('--sub-orb-2-size');
        root.style.removeProperty('--sub-orb-3-size');
        root.style.removeProperty('--sub-orb-1-x');
        root.style.removeProperty('--sub-orb-1-y');
        root.style.removeProperty('--sub-orb-2-x');
        root.style.removeProperty('--sub-orb-2-y');
        root.style.removeProperty('--sub-orb-3-x');
        root.style.removeProperty('--sub-orb-3-y');
    };
};

window.stopSubmissionsOrbs = function () {
    if (window._submissionsOrbsCleanup) {
        window._submissionsOrbsCleanup();
        window._submissionsOrbsCleanup = null;
    }
};
