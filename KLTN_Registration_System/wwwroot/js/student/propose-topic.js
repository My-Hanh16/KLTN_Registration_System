let pendingSubmitForm = null;

function updateTitleCounter() {
    const len = document.getElementById('titleInput').value.length;
    const el = document.getElementById('titleCt');
    el.textContent = len + ' / 200';
    el.classList.toggle('warn', len > 170);
}

function updateDescCounter() {
    const len = document.getElementById('descInput').value.length;
    const el = document.getElementById('descCt');
    el.textContent = len + ' / 1000';
    el.classList.toggle('warn', len > 850);
}

function resetForm() {
    document.querySelectorAll('.cat-chip').forEach(c => c.classList.remove('active'));
    document.getElementById('catInput').value = '';
    document.getElementById('titleCt').textContent = '0 / 200';
    document.getElementById('descCt').textContent = '0 / 1000';
    ['titleCt', 'descCt'].forEach(id => document.getElementById(id).classList.remove('warn'));
}

function pageToast(message, type = 'error') {
    const oldToast = document.querySelector('.pt-toast');
    oldToast?.remove();

    const toast = document.createElement('div');
    toast.className = `pt-toast ${type}`;
    toast.innerHTML = `
        <i class="fa-solid ${type === 'success' ? 'fa-circle-check' : 'fa-circle-exclamation'}"></i>
        <span>${message}</span>
    `;
    document.body.appendChild(toast);

    window.setTimeout(() => {
        toast.classList.add('is-hiding');
        window.setTimeout(() => toast.remove(), 180);
    }, 2600);
}

function openConfirmModal({ title, message, confirmText, danger = false, onConfirm }) {
    const modal = document.getElementById('proposalConfirmModal');
    if (!modal) return;

    document.getElementById('proposalConfirmTitle').textContent = title;
    document.getElementById('proposalConfirmMessage').textContent = message;
    const confirmBtn = document.getElementById('proposalConfirmBtn');
    confirmBtn.innerHTML = `<i class="fa-solid ${danger ? 'fa-trash' : 'fa-paper-plane'}"></i> ${confirmText}`;
    confirmBtn.classList.toggle('danger', danger);
    confirmBtn.onclick = onConfirm;

    modal.classList.add('open');
    document.body.style.overflow = 'hidden';
}

function closeConfirmModal() {
    const modal = document.getElementById('proposalConfirmModal');
    if (!modal) return;
    modal.classList.remove('open');
    document.body.style.overflow = '';
}

function setSendingState() {
    const btn = document.getElementById('sendBtn');
    btn.disabled = true;
    document.getElementById('btnSpinner').style.display = 'block';
    document.getElementById('btnIco').style.display = 'none';
    document.getElementById('btnTxt').textContent = 'Đang gửi...';
}

function validateProposalForm() {
    const titleInput = document.getElementById('titleInput');
    const descInput = document.getElementById('descInput');
    const title = titleInput.value.trim();
    const desc = descInput.value.trim();

    if (!title) {
        pageToast('Vui lòng nhập tên đề tài.');
        titleInput.focus();
        return null;
    }

    if (!desc) {
        pageToast('Vui lòng nhập mô tả ý tưởng.');
        descInput.focus();
        return null;
    }

    return { title, desc };
}

document.addEventListener('DOMContentLoaded', () => {
    const ti = document.getElementById('titleInput');
    const di = document.getElementById('descInput');
    const form = document.getElementById('proposeForm');
    const resetButton = document.getElementById('resetBtn');

    document.querySelectorAll('[data-quota-progress]').forEach((bar) => {
        const progress = Number(bar.dataset.quotaProgress || '0');
        const normalized = Math.min(100, Math.max(0, progress));
        bar.style.width = `${normalized}%`;
    });

    document.querySelectorAll('.cat-chip').forEach((chip) => {
        chip.addEventListener('click', () => {
            if (chip.classList.contains('disabled')) return;

            document.querySelectorAll('.cat-chip').forEach(c => c.classList.remove('active'));
            chip.classList.add('active');
            document.getElementById('catInput').value = chip.dataset.val || '';
        });
    });

    if (ti) {
        ti.addEventListener('input', () => {
            if (ti.value.length > 200) ti.value = ti.value.substring(0, 200);
            updateTitleCounter();
        });
    }

    if (di) {
        di.addEventListener('input', () => {
            if (di.value.length > 1000) di.value = di.value.substring(0, 1000);
            updateDescCounter();
        });
    }

    if (form) {
        form.addEventListener('submit', (event) => {
            if (pendingSubmitForm === form) {
                pendingSubmitForm = null;
                setSendingState();
                return;
            }

            event.preventDefault();
            const valid = validateProposalForm();
            if (!valid) return;

            openConfirmModal({
                title: 'Xác nhận gửi đề xuất',
                message: `"${valid.title}" - Admin sẽ xem xét trong 3-5 ngày làm việc.`,
                confirmText: 'Gửi đề xuất',
                onConfirm: () => {
                    closeConfirmModal();
                    pendingSubmitForm = form;
                    form.requestSubmit();
                }
            });
        });
    }

    if (resetButton) {
        resetButton.addEventListener('click', resetForm);
    }

    document.querySelectorAll('.cancel-proposal-form').forEach((cancelForm) => {
        cancelForm.addEventListener('submit', (event) => {
            event.preventDefault();
            const title = cancelForm.dataset.title || 'đề xuất này';

            openConfirmModal({
                title: 'Huỷ đề xuất',
                message: `Bạn có chắc muốn huỷ "${title}"?`,
                confirmText: 'Huỷ đề xuất',
                danger: true,
                onConfirm: () => {
                    closeConfirmModal();
                    cancelForm.submit();
                }
            });
        });
    });

    document.querySelectorAll('[data-close-proposal-confirm]').forEach((button) => {
        button.addEventListener('click', closeConfirmModal);
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') closeConfirmModal();
    });
});
