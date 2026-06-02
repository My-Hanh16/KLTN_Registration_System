(function () {
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.slot-fill[data-slot-pct]').forEach(function (bar) {
            const pct = Math.max(0, Math.min(100, Number(bar.dataset.slotPct || 0)));
            bar.style.width = pct + '%';
        });
    });
})();
