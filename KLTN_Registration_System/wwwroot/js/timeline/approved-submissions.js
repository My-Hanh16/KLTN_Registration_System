document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.js-auto-submit').forEach((select) => {
        select.addEventListener('change', () => {
            if (select.name === 'faculty') {
                const majorSelect = select.form?.querySelector('select[name="majorId"]');
                if (majorSelect) {
                    majorSelect.value = '';
                }
            }

            select.form?.submit();
        });
    });
});
