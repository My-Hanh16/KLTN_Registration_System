document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.js-auto-submit').forEach((select) => {
        select.addEventListener('change', () => {
            select.form?.submit();
        });
    });
});
