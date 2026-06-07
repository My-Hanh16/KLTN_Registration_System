(function () {
    let lockFormId = null;
    let deleteFormId = null;
    let resetPassword = '';

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

    function copyResetPassword(button) {
        if (!resetPassword) return;
        navigator.clipboard?.writeText(resetPassword);

        const old = button.innerText;
        button.innerText = 'Copied';
        setTimeout(() => {
            button.innerText = old;
        }, 1800);
    }

    function showResetSuccessToast(password) {
        document.getElementById('toastResetAjax')?.remove();

        const toast = document.createElement('div');
        toast.className = 'toast show';
        toast.id = 'toastResetAjax';
        toast.innerHTML = `
            <div class="toast-body">
                <div class="toast-icon green"><i class="fa-solid fa-key"></i></div>
                <div class="toast-content">
                    <div class="toast-title">Reset mật khẩu thành công</div>
                    <div class="toast-msg">Mật khẩu mới:</div>
                    <div class="toast-pwd-box">
                        <span class="toast-pwd" id="ajaxResetPwd"></span>
                        <button type="button" class="btn-copy" data-copy-ajax-reset-password>Copy</button>
                    </div>
                </div>
                <button type="button" class="toast-close" data-close-toast="toastResetAjax"><i class="fa-solid fa-xmark"></i></button>
            </div>
            <div class="toast-bar green"></div>
        `;

        document.body.appendChild(toast);
        document.getElementById('ajaxResetPwd').textContent = password;
        toast.querySelector('[data-close-toast]')?.addEventListener('click', () => closeToast('toastResetAjax'));
        toast.querySelector('[data-copy-ajax-reset-password]')?.addEventListener('click', event => {
            copyResetPassword(event.currentTarget);
        });

        setTimeout(() => closeToast('toastResetAjax'), 7000);
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

    function openResetModal(button) {
        resetPassword = '';
        document.getElementById('resetUserId').value = button.dataset.resetUserId || '';
        document.getElementById('resetSub').textContent = [
            button.dataset.resetUserName,
            button.dataset.resetUserEmail || button.dataset.resetUserCode
        ].filter(Boolean).join(' · ');

        document.getElementById('resetNewPassword').value = '';
        document.getElementById('resetResult')?.classList.add('is-hidden');
        document.getElementById('resetTempPassword').textContent = '';

        const randomRadio = document.querySelector('#resetPasswordForm input[name="useRandomPassword"][value="true"]');
        if (randomRadio) {
            randomRadio.checked = true;
            updateResetMode();
        }

        openModal('resetModal');
    }

    function updateResetMode() {
        const form = document.getElementById('resetPasswordForm');
        const custom = form?.querySelector('input[name="useRandomPassword"][value="false"]')?.checked;
        document.getElementById('resetCustomPasswordWrap')?.classList.toggle('is-hidden', !custom);
        document.querySelectorAll('.reset-mode-card').forEach(card => {
            const radio = card.querySelector('input[type="radio"]');
            card.classList.toggle('checked', !!radio?.checked);
        });
    }

    async function submitReset(event) {
        event.preventDefault();

        const form = event.currentTarget;
        const button = document.getElementById('resetSubmit');
        const oldText = button?.innerHTML;
        if (button) {
            button.disabled = true;
            button.innerHTML = '<i class="fa-solid fa-spinner fa-spin modal-button-icon"></i> Đang reset';
        }

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });
            const data = await response.json();

            if (!data.success) {
                alert(data.message || 'Không thể reset mật khẩu.');
                return;
            }

            resetPassword = data.tempPassword || '';
            document.getElementById('resetTempPassword').textContent = resetPassword;
            document.getElementById('resetResult')?.classList.add('is-hidden');
            document.getElementById('resetNewPassword').value = '';
            closeModal('resetModal');
            showResetSuccessToast(resetPassword);
        } catch {
            alert('Không thể kết nối máy chủ. Vui lòng thử lại.');
        } finally {
            if (button) {
                button.disabled = false;
                button.innerHTML = oldText;
            }
        }
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

        const inlineImportBtn = document.getElementById('inlineImportUsersBtn');
        const inlineImportInput = document.getElementById('inlineImportUsersInput');
        const inlineImportForm = document.getElementById('inlineImportUsersForm');

        inlineImportBtn?.addEventListener('click', () => {
            inlineImportInput?.click();
        });

        inlineImportInput?.addEventListener('change', () => {
            if (!inlineImportInput.files.length || !inlineImportForm) return;

            inlineImportBtn.disabled = true;
            inlineImportBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Đang nhập...';
            inlineImportForm.submit();
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

        document.querySelectorAll('[data-reset-user-id]').forEach(button => {
            button.addEventListener('click', () => openResetModal(button));
        });

        document.querySelectorAll('.reset-mode-card').forEach(card => {
            card.addEventListener('click', () => {
                const radio = card.querySelector('input[type="radio"]');
                if (radio) radio.checked = true;
                updateResetMode();
            });
        });

        document.getElementById('resetPasswordForm')?.addEventListener('submit', submitReset);
        document.querySelector('[data-copy-reset-password]')?.addEventListener('click', event => {
            copyResetPassword(event.currentTarget);
        });

        document.querySelectorAll('.js-confirm-submit').forEach(form => {
            form.addEventListener('submit', async event => {
                event.preventDefault();
                if (await confirmSubmit(form)) form.submit();
            });
        });

        document.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                ['roleModal', 'lockModal', 'deleteModal', 'resetModal', 'createModal'].forEach(closeModal);
            }
        });

        setTimeout(() => closeToast('toastPwd'), 7000);
        setTimeout(() => closeToast('toastOk'), 4000);
        setTimeout(() => closeToast('toastErr'), 4000);
    });
})();
