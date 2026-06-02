(function () {
    function updateToggleLabel(input) {
        const wrap = input.closest('.toggle-wrap');
        const label = wrap?.querySelector('.toggle-label');
        if (!label) return;

        label.textContent = input.checked ? 'ĐANG MỞ' : 'ĐANG ĐÓNG';
        label.classList.toggle('on', input.checked);
        label.classList.toggle('off', !input.checked);
    }

    function bindToggleLabels() {
        document.querySelectorAll('.js-toggle-status').forEach(input => {
            input.addEventListener('change', () => updateToggleLabel(input));
            updateToggleLabel(input);
        });
    }

    function bindRangeValue() {
        const range = document.getElementById('rangeInput');
        const value = document.getElementById('rangeVal');
        if (!range || !value) return;

        range.addEventListener('input', () => {
            value.textContent = range.value;
        });
    }

    async function confirmAction(message) {
        if (window.Swal) {
            const result = await Swal.fire({
                icon: 'question',
                title: 'Xác nhận thao tác?',
                text: message,
                showCancelButton: true,
                confirmButtonText: 'Đồng ý',
                cancelButtonText: 'Huỷ',
                confirmButtonColor: '#2563eb',
                cancelButtonColor: '#94a3b8'
            });

            return result.isConfirmed;
        }

        return window.confirm(message);
    }

    function bindConfirmButtons() {
        document.querySelectorAll('[data-confirm-message]').forEach(button => {
            button.addEventListener('click', async event => {
                const message = button.dataset.confirmMessage;
                if (!message) return;

                event.preventDefault();
                const accepted = await confirmAction(message);
                if (accepted) {
                    button.closest('form')?.requestSubmit(button);
                }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        bindToggleLabels();
        bindRangeValue();
        bindConfirmButtons();
    });
})();
