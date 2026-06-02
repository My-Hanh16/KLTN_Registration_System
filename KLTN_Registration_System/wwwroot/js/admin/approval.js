(function () {
    async function confirmWithSwal(options) {
        if (window.Swal) {
            const result = await Swal.fire({
                icon: options.icon || 'question',
                title: options.title,
                text: options.text,
                showCancelButton: true,
                confirmButtonText: options.confirmText || 'Đồng ý',
                cancelButtonText: 'Huỷ',
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
            text: 'Hệ thống sẽ tự bỏ qua sinh viên trùng hoặc đề tài đã đủ chỗ.',
            confirmText: 'Duyệt tất cả',
            confirmColor: '#16a34a'
        });

        bindConfirm('.js-reject-registration-form', {
            icon: 'warning',
            title: 'Từ chối đăng ký này?',
            text: 'Sinh viên sẽ nhận trạng thái từ chối cho yêu cầu đăng ký này.',
            confirmText: 'Từ chối',
            confirmColor: '#dc2626'
        });
    });
})();
