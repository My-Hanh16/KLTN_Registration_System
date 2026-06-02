document.addEventListener('DOMContentLoaded', () => {
    const form = document.querySelector('.proposal-form');
    if (!form) return;

    form.addEventListener('submit', () => {
        const submitButton = form.querySelector('[data-submit-proposal]');
        if (!submitButton || submitButton.disabled) return;

        submitButton.disabled = true;
        submitButton.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Đang gửi đề xuất...';
    });
});
