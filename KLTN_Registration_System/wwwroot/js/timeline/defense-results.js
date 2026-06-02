document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-progress]').forEach((bar) => {
        const value = Number(bar.dataset.progress || '0');
        const normalized = Math.min(100, Math.max(0, value));
        bar.style.width = `${normalized}%`;
    });

    document.querySelectorAll('.js-confirm-form').forEach((form) => {
        form.addEventListener('submit', (event) => {
            const message = form.dataset.confirm;
            if (message && !confirm(message)) {
                event.preventDefault();
            }
        });
    });
});
