(function () {
    const typeCfg = {
        System: { ico: 'fa-regular fa-bell', c: '#2563eb', bg: '#dbeafe' },
        SystemAlert: { ico: 'fa-solid fa-triangle-exclamation', c: '#d97706', bg: '#fef3c7' },
        Deadline: { ico: 'fa-regular fa-clock', c: '#db2777', bg: '#fce7f3' }
    };

    function $(id) {
        return document.getElementById(id);
    }

    function escapeHtml(value) {
        return String(value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function selectTarget(value) {
        document.querySelectorAll('.tg-card').forEach(card => card.classList.remove('sel'));
        const card = $('tc-' + value);
        if (!card) return;

        card.classList.add('sel');
        const radio = card.querySelector('input[type="radio"]');
        if (radio) radio.checked = true;
    }

    function onTypeChange() {
        const select = $('typeSelect');
        const previewIcon = $('previewIco');
        if (!select || !previewIcon) return;

        const cfg = typeCfg[select.value] || typeCfg.System;
        previewIcon.style.background = cfg.bg;
        previewIcon.style.color = cfg.c;
        previewIcon.innerHTML = `<i class="${cfg.ico}"></i>`;
    }

    function syncPreview() {
        const title = $('titleInput')?.value.trim() || '';
        const content = $('contentInput')?.value.trim() || '';
        const previewTitle = $('previewTitle');
        const previewBody = $('previewBody');

        if (previewTitle) {
            previewTitle.innerHTML = title
                ? `<strong>${escapeHtml(title)}</strong>`
                : '<span class="placeholder-tx">Tiêu đề sẽ hiển thị ở đây...</span>';
        }

        if (previewBody) {
            previewBody.innerHTML = content
                ? escapeHtml(content)
                : '<span class="placeholder-tx">Nội dung thông báo...</span>';
        }
    }

    function togglePreview() {
        const box = $('previewBox');
        const btn = $('btnPreview');
        if (!box || !btn) return;

        const show = box.style.display === 'none' || box.style.display === '';
        box.style.display = show ? 'block' : 'none';
        btn.innerHTML = show
            ? '<i class="fa-regular fa-eye-slash"></i> Ẩn xem trước'
            : '<i class="fa-regular fa-eye"></i> Xem trước';

        if (show) {
            syncPreview();
            onTypeChange();
        }
    }

    function updateTitleCount() {
        const input = $('titleInput');
        const counter = $('titleCt');
        if (!input || !counter) return;

        if (input.value.length > 120) input.value = input.value.substring(0, 120);
        counter.textContent = input.value.length + ' / 120';
        counter.classList.toggle('warn', input.value.length > 100);
        syncPreview();
    }

    function updateContentCount() {
        const input = $('contentInput');
        const counter = $('contentCt');
        if (!input || !counter) return;

        if (input.value.length > 500) input.value = input.value.substring(0, 500);
        counter.textContent = input.value.length + ' / 500';
        counter.classList.toggle('warn', input.value.length > 420);
        syncPreview();
    }

    async function confirmSend(event) {
        event.preventDefault();

        const titleInput = $('titleInput');
        const contentInput = $('contentInput');
        const title = titleInput?.value.trim() || '';
        const content = contentInput?.value.trim() || '';

        if (!title) {
            await Swal.fire({
                icon: 'warning',
                title: 'Thiếu tiêu đề',
                text: 'Vui lòng nhập tiêu đề thông báo.'
            });
            titleInput?.focus();
            return;
        }

        if (!content) {
            await Swal.fire({
                icon: 'warning',
                title: 'Thiếu nội dung',
                text: 'Vui lòng nhập nội dung thông báo.'
            });
            contentInput?.focus();
            return;
        }

        const target = document.querySelector('input[name="target"]:checked')?.value;
        const targetLabel = {
            all: 'tất cả người dùng',
            student: 'sinh viên',
            lecturer: 'giảng viên'
        }[target] || 'người dùng';

        const result = await Swal.fire({
            title: 'Xác nhận gửi?',
            html: `
                <div class="swal-confirm-detail">
                    <b>Tiêu đề:</b> ${escapeHtml(title)}<br><br>
                    <b>Người nhận:</b> ${escapeHtml(targetLabel)}<br><br>
                    <span class="swal-confirm-warning">Hành động này không thể hoàn tác.</span>
                </div>
            `,
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Gửi thông báo',
            cancelButtonText: 'Huỷ',
            confirmButtonColor: '#2563eb',
            cancelButtonColor: '#94a3b8'
        });

        if (!result.isConfirmed) return;

        const btn = $('sendBtn');
        if (btn) btn.disabled = true;
        if ($('sendSpinner')) $('sendSpinner').style.display = 'block';
        if ($('sendIco')) $('sendIco').style.display = 'none';
        if ($('sendTxt')) $('sendTxt').textContent = 'Đang gửi...';

        $('bcForm')?.submit();
    }

    async function confirmDeleteOld(event) {
        event.preventDefault();

        const result = await Swal.fire({
            title: 'Xóa thông báo cũ?',
            text: 'Hành động này không thể hoàn tác.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Xóa',
            cancelButtonText: 'Huỷ',
            confirmButtonColor: '#dc2626'
        });

        if (result.isConfirmed) {
            event.target.closest('form')?.submit();
        }
    }

    function showFlashMessage() {
        const el = $('broadcastFlash');
        if (!el) return;

        let flash;
        try {
            flash = JSON.parse(el.textContent || '{}');
        } catch {
            flash = {};
        }

        if (flash.success) {
            Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'success',
                title: flash.success,
                showConfirmButton: false,
                timer: 2500,
                timerProgressBar: true
            });
        }

        if (flash.error) {
            Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'error',
                title: flash.error,
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true
            });
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.tg-card[data-target-value]').forEach(card => {
            card.addEventListener('click', () => selectTarget(card.dataset.targetValue));
        });

        $('typeSelect')?.addEventListener('change', onTypeChange);
        $('btnPreview')?.addEventListener('click', togglePreview);
        $('titleInput')?.addEventListener('input', updateTitleCount);
        $('contentInput')?.addEventListener('input', updateContentCount);
        $('bcForm')?.addEventListener('submit', confirmSend);

        document.querySelectorAll('.js-delete-old-form').forEach(form => {
            form.addEventListener('submit', confirmDeleteOld);
        });

        updateTitleCount();
        updateContentCount();
        onTypeChange();
        showFlashMessage();
    });
})();
