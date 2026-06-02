(function () {
    let lockFormId = null;
    let deleteFormId = null;

    function openModal(id) {
        document.getElementById(id)?.classList.add('open');
        document.body.style.overflow = 'hidden';
    }

    function closeModal(id) {
        document.getElementById(id)?.classList.remove('open');
        document.body.style.overflow = '';
    }

    function closeToast(id) {
        const el = document.getElementById(id);
        if (!el) return;

        el.style.transition = 'opacity .3s, transform .3s';
        el.style.opacity = '0';
        el.style.transform = 'translateX(30px)';
        setTimeout(() => el.remove(), 320);
    }

    function copyTempPassword(button) {
        const password = document.getElementById('tempPwd')?.innerText || '';
        navigator.clipboard?.writeText(password);

        const old = button.innerText;
        button.innerText = '✓ Đã copy';
        setTimeout(() => {
            button.innerText = old;
        }, 2000);
    }

    function openRoleModal(button) {
        const uid = button.dataset.roleUserId || '';
        const name = button.dataset.roleUserName || '';
        const currentRole = button.dataset.roleCurrent || '';

        document.getElementById('roleModalUid').value = uid;
        document.getElementById('roleModalName').textContent = name;

        document.querySelectorAll('#roleRadioWrap .role-radio-card').forEach(card => {
            const radio = card.querySelector('input[type="radio"]');
            const match = radio?.value === currentRole;
            if (radio) radio.checked = match;
            card.classList.toggle('checked', match);
        });

        openModal('roleModal');
    }

    function pickRole(label) {
        document.querySelectorAll('.role-radio-card').forEach(card => card.classList.remove('checked'));
        label.classList.add('checked');
        const radio = label.querySelector('input[type="radio"]');
        if (radio) radio.checked = true;
    }

    function openLockModal(button) {
        lockFormId = button.dataset.lockFormId || null;
        const name = button.dataset.lockUserName || '';
        const isLocked = button.dataset.isLocked === 'True';
        const locking = !isLocked;

        document.getElementById('lockTitle').textContent = locking ? 'Khóa tài khoản' : 'Mở khóa tài khoản';
        document.getElementById('lockSub').textContent = name;
        document.getElementById('lockInfo').innerHTML = locking
            ? `Bạn có chắc muốn <strong class="lock-danger">khóa</strong> tài khoản <strong>${name}</strong>? Người dùng sẽ không thể đăng nhập.`
            : `Bạn có chắc muốn <strong class="lock-success">mở khóa</strong> tài khoản <strong>${name}</strong>? Người dùng có thể đăng nhập trở lại.`;

        const icon = document.getElementById('lockIco');
        icon.className = 'm-ico ' + (locking ? 'red' : 'green');
        icon.innerHTML = locking ? '<i class="fa-solid fa-lock"></i>' : '<i class="fa-solid fa-lock-open"></i>';

        const btn = document.getElementById('lockConfirm');
        btn.className = 'btn-m ' + (locking ? 'red' : 'green');

        openModal('lockModal');
    }

    function submitLock() {
        if (lockFormId) document.getElementById(lockFormId)?.submit();
    }

    function openDeleteModal(button) {
        deleteFormId = button.dataset.deleteFormId || null;
        document.getElementById('deleteSub').textContent = button.dataset.deleteUserName || '';
        openModal('deleteModal');
    }

    function submitDelete() {
        if (deleteFormId) document.getElementById(deleteFormId)?.submit();
    }

    async function confirmSubmit(form) {
        const message = form.dataset.confirmMessage || 'Xác nhận thao tác này?';
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

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-close-toast]').forEach(button => {
            button.addEventListener('click', () => closeToast(button.dataset.closeToast));
        });

        document.querySelector('[data-copy-temp-password]')?.addEventListener('click', event => {
            copyTempPassword(event.currentTarget);
        });

        document.querySelectorAll('[data-open-modal]').forEach(button => {
            button.addEventListener('click', () => openModal(button.dataset.openModal));
        });

        document.querySelectorAll('[data-close-modal]').forEach(button => {
            button.addEventListener('click', () => closeModal(button.dataset.closeModal));
        });

        document.querySelectorAll('[data-dismiss-modal]').forEach(overlay => {
            overlay.addEventListener('click', event => {
                if (event.target === overlay) closeModal(overlay.dataset.dismissModal);
            });
        });

        document.querySelectorAll('.js-auto-submit').forEach(select => {
            select.addEventListener('change', () => select.form?.submit());
        });

        document.querySelectorAll('[data-role-user-id]').forEach(button => {
            button.addEventListener('click', () => openRoleModal(button));
        });

        document.querySelectorAll('.role-radio-card').forEach(card => {
            card.addEventListener('click', () => pickRole(card));
        });

        document.querySelectorAll('[data-lock-form-id]').forEach(button => {
            button.addEventListener('click', () => openLockModal(button));
        });

        document.getElementById('lockConfirm')?.addEventListener('click', submitLock);

        document.querySelectorAll('[data-delete-form-id]').forEach(button => {
            button.addEventListener('click', () => openDeleteModal(button));
        });

        document.getElementById('deleteConfirm')?.addEventListener('click', submitDelete);

        document.querySelectorAll('.js-confirm-submit').forEach(form => {
            form.addEventListener('submit', async event => {
                event.preventDefault();
                if (await confirmSubmit(form)) form.submit();
            });
        });

        document.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                ['roleModal', 'lockModal', 'deleteModal', 'createModal'].forEach(closeModal);
            }
        });

        setTimeout(() => closeToast('toastPwd'), 7000);
        setTimeout(() => closeToast('toastOk'), 4000);
        setTimeout(() => closeToast('toastErr'), 4000);
    });
})();
