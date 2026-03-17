// ShellSpecter — Lightweight Canvas Chart Rendering
// No external dependencies — pure canvas API

window.shellSpecterCharts = {

    /**
     * Draw a sparkline on a canvas element.
     * @param {string} canvasId - Canvas element ID
     * @param {number[]} data - Array of values (0-100)
     * @param {string} color - CSS color for the line
     * @param {string} fillColor - CSS color for the area fill (with alpha)
     */
    drawSparkline: function (canvasId, data, color, fillColor) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        const dpr = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();

        canvas.width = rect.width * dpr;
        canvas.height = rect.height * dpr;
        ctx.scale(dpr, dpr);

        const w = rect.width;
        const h = rect.height;

        ctx.clearRect(0, 0, w, h);

        if (!data || data.length < 2) return;

        const maxVal = 100;
        const step = w / (data.length - 1);

        // Draw gradient fill
        ctx.beginPath();
        ctx.moveTo(0, h);

        for (let i = 0; i < data.length; i++) {
            const x = i * step;
            const y = h - (data[i] / maxVal) * (h - 2);
            if (i === 0) ctx.lineTo(x, y);
            else ctx.lineTo(x, y);
        }

        ctx.lineTo(w, h);
        ctx.closePath();

        const gradient = ctx.createLinearGradient(0, 0, 0, h);
        gradient.addColorStop(0, fillColor || 'rgba(0, 229, 255, 0.15)');
        gradient.addColorStop(1, 'rgba(0, 229, 255, 0.01)');
        ctx.fillStyle = gradient;
        ctx.fill();

        // Draw line
        ctx.beginPath();
        for (let i = 0; i < data.length; i++) {
            const x = i * step;
            const y = h - (data[i] / maxVal) * (h - 2);
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        }

        ctx.strokeStyle = color || '#00e5ff';
        ctx.lineWidth = 1.5;
        ctx.lineJoin = 'round';
        ctx.lineCap = 'round';
        ctx.stroke();

        // Draw dot at the end
        if (data.length > 0) {
            const lastX = (data.length - 1) * step;
            const lastY = h - (data[data.length - 1] / maxVal) * (h - 2);
            ctx.beginPath();
            ctx.arc(lastX, lastY, 2.5, 0, Math.PI * 2);
            ctx.fillStyle = color || '#00e5ff';
            ctx.fill();
            ctx.beginPath();
            ctx.arc(lastX, lastY, 5, 0, Math.PI * 2);
            ctx.fillStyle = (fillColor || 'rgba(0, 229, 255, 0.3)');
            ctx.fill();
        }
    },

    /**
     * Draw a gauge arc on a canvas element.
     * @param {string} canvasId - Canvas element ID
     * @param {number} value - Current value (0-100)
     * @param {string} color - CSS color for the active arc
     * @param {string} trackColor - CSS color for the background track
     */
    drawGauge: function (canvasId, value, color, trackColor) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        const dpr = window.devicePixelRatio || 1;
        const size = Math.min(canvas.parentElement.offsetWidth, 120);

        canvas.width = size * dpr;
        canvas.height = (size * 0.65) * dpr;
        canvas.style.width = size + 'px';
        canvas.style.height = (size * 0.65) + 'px';
        ctx.scale(dpr, dpr);

        const w = size;
        const h = size * 0.65;
        const cx = w / 2;
        const cy = h - 4;
        const radius = Math.min(cx, cy) - 6;
        const lineWidth = 8;
        const startAngle = Math.PI;
        const endAngle = 2 * Math.PI;
        const valueAngle = startAngle + (value / 100) * Math.PI;

        ctx.clearRect(0, 0, w, h);

        // Track
        ctx.beginPath();
        ctx.arc(cx, cy, radius, startAngle, endAngle);
        ctx.strokeStyle = trackColor || 'rgba(255,255,255,0.06)';
        ctx.lineWidth = lineWidth;
        ctx.lineCap = 'round';
        ctx.stroke();

        // Value arc
        ctx.beginPath();
        ctx.arc(cx, cy, radius, startAngle, valueAngle);
        ctx.strokeStyle = color || '#00e5ff';
        ctx.lineWidth = lineWidth;
        ctx.lineCap = 'round';
        ctx.stroke();

        // Glow effect
        ctx.beginPath();
        ctx.arc(cx, cy, radius, startAngle, valueAngle);
        ctx.strokeStyle = color || '#00e5ff';
        ctx.lineWidth = lineWidth + 4;
        ctx.lineCap = 'round';
        ctx.globalAlpha = 0.15;
        ctx.stroke();
        ctx.globalAlpha = 1;
    }
};
