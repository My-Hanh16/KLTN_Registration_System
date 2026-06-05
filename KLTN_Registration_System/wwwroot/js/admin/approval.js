(function () {
    async function confirmWithSwal(options) {
        if (window.Swal) {
            const result = await Swal.fire({
                icon: options.icon || 'question',
                title: options.title,
                text: options.text,
                showCancelButton: true,
                confirmButtonText: options.confirmText || 'Đồng ý',
                cancelButtonText: 'Hủy',
                confirmButtonColor: options.confirmColor || '#4f46e5',
                cancelButtonColor: '#94a3b8'
            });

            return result.isConfirmed;
        }

        return window.confirm(options.text || options.title);
    }

    function bindConfirm(formSelector, options) {
        document.querySelectorAll(formSelector).forEach(form => {
            form.addEventListener('submit', async function (event) {
                event.preventDefault();
                const accepted = await confirmWithSwal(options);
                if (accepted) {
                    form.submit();
                }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        bindConfirm('.js-approve-all-form', {
            icon: 'question',
            title: 'Duyệt tất cả đăng ký?',
            text: 'Hệ thống sẽ duyệt theo từng nhóm, nhóm nào có sinh viên trùng hoặc đề tài không đủ chỗ sẽ bị bỏ qua cả nhóm.',
            confirmText: 'Duyệt tất cả',
            confirmColor: '#16a34a'
        });

        bindConfirm('.js-reject-registration-form', {
            icon: 'warning',
            title: 'Từ chối nhóm đăng ký này?',
            text: 'Tất cả sinh viên trong cùng nhóm đăng ký sẽ nhận trạng thái từ chối.',
            confirmText: 'Từ chối',
            confirmColor: '#dc2626'
        });
    });
})();
