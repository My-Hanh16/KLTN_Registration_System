document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.topic-progress-fill').forEach((bar) => {
        const progress = Number(bar.dataset.progress || '0');
        const safeProgress = Math.min(100, Math.max(0, progress));
        bar.style.width = `${safeProgress}%`;
    });
});
