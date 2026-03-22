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
