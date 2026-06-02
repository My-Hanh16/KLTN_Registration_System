document.addEventListener('DOMContentLoaded', () => {
            document.querySelectorAll('.toast-item').forEach(t => {
                requestAnimationFrame(() => t.classList.add('show'));
                setTimeout(() => removeToast(t.id), 4500);
            });
        });

        function removeToast(id) {
            const el = document.getElementById(id);
            if (!el) return;
            el.classList.remove('show');
            setTimeout(() => el.remove(), 350);
        }

        document.addEventListener('click', (event) => {
            const button = event.target.closest('.js-toast-close');
            if (!button) return;
            removeToast(button.dataset.toastId);
        });

        // Dùng cho các view cũ gọi showToast()
        function showToast(message, type = 'success') {
            const wrap = document.getElementById('toastWrap');
            const id   = 'toast_' + Date.now();
            const isErr = type === 'error';
            wrap.insertAdjacentHTML('beforeend', `
                <div class="toast-item ${isErr ? 'error' : 'success'}" id="${id}">
                    <div class="toast-icon">
                        <i class="fa-solid ${isErr ? 'fa-circle-exclamation' : 'fa-circle-check'}"></i>
                    </div>
                    <div class="toast-body">
                        <div class="t-title">${isErr ? 'Có lỗi xảy ra' : 'Thành công'}</div>
                        <div class="t-msg">${message}</div>
                    </div>
                    <button class="toast-close js-toast-close" data-toast-id="${id}">
                        <i class="fa-solid fa-xmark"></i>
                    </button>
                </div>`);
            const el = document.getElementById(id);
            requestAnimationFrame(() => el?.classList.add('show'));
            setTimeout(() => removeToast(id), 4000);
        }

        const dashboardMenuBtn = document.getElementById('dashboardMenuBtn');
        const closeSidebarEls = document.querySelectorAll('[data-close-sidebar]');

        dashboardMenuBtn?.addEventListener('click', () => {
            document.body.classList.toggle('sidebar-open');
        });

        closeSidebarEls.forEach((el) => {
            el.addEventListener('click', () => document.body.classList.remove('sidebar-open'));
        });

        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') {
                document.body.classList.remove('sidebar-open');
            }
        });
