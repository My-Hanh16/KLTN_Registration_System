let _token = null;

    function getToken() {
        if (!_token) {
            _token = document.querySelector('#createTopicForm input[name="__RequestVerificationToken"], #editTopicForm input[name="__RequestVerificationToken"]')?.value ?? '';
        }
        return _token;
    }
    const topicListConfig = document.getElementById('topic-list-config');
    const CREATE_URL = topicListConfig?.dataset.createUrl || '/Lecturer/Create';
    const EDIT_URL = topicListConfig?.dataset.editUrl || '/Admin/EditTopic';
    const IS_ADMIN = topicListConfig?.dataset.isAdmin === 'true';

    function filterByStatus(status) {
        document.getElementById('statusInput').value = status;
        document.getElementById('filterForm').submit();
    }

    function openCreateModal() {
        document.getElementById('createModal').classList.add('open');
        document.body.style.overflow = 'hidden';
    }

    function closeCreateModal() {
        document.getElementById('createModal').classList.remove('open');
        document.body.style.overflow = '';
        document.getElementById('createTopicForm').reset();
    }

    function openEditModal(button) {
        const form = document.getElementById('editTopicForm');
        if (!form) return;

        form.reset();
        form.Id.value = button.dataset.id || '';
        form.Title.value = button.dataset.title || '';
        form.Description.value = button.dataset.description || '';
        form.LecturerId.value = button.dataset.lecturerId || '';
        form.MajorId.value = button.dataset.majorId || '';
        form.Level.value = button.dataset.level || 'Easy';
        form.MaxStudents.value = button.dataset.maxStudents || 1;
        form.Status.value = button.dataset.status || 'Pending';
        form.Deadline.value = button.dataset.deadline || '';
        form.Category.value = button.dataset.category || 'Ứng dụng';
        form.Note.value = button.dataset.note || '';
        form.IsApproved.checked = button.dataset.isApproved === 'true';
        form.IsRegistrationOpen.checked = button.dataset.isRegistrationOpen === 'true';

        document.getElementById('editModal').classList.add('open');
        document.body.style.overflow = 'hidden';
    }

    function closeEditModal() {
        const modal = document.getElementById('editModal');
        if (!modal) return;
        modal.classList.remove('open');
        document.body.style.overflow = '';
        document.getElementById('editTopicForm')?.reset();
    }

    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') {
            closeCreateModal();
            closeEditModal();
        }
    });

    // ── TOAST — Fix: đổi tên thành pageToast để tránh xung đột với layout cha showToast() ──
    function pageToast(message, type = 'success') {
        Swal.fire({
            toast: true,
            position: 'top-end',
            icon: type,
            title: message,
            showConfirmButton: false,
            timer: 2500,
            timerProgressBar: true
        });
    }

    // ── IMPORT EXCEL ──
    function handleFileSelect() {
        const fileInput = document.getElementById('excelInput');
        if (!fileInput.files.length) return;

        const btn = document.getElementById('importBtn');
        btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Đang nhập...';
        btn.disabled  = true;
        document.getElementById('importExcelForm').submit();
    }

    // ── CREATE TOPIC ──
    async function submitCreateTopic(e) {
        e.preventDefault();

        const form      = document.getElementById('createTopicForm');
        const submitBtn = document.getElementById('submitCreateBtn');

        submitBtn.disabled    = true;
        submitBtn.innerHTML   = '<i class="fa-solid fa-spinner fa-spin"></i> Đang tạo...';

        const lecSelect = form.querySelector('[name="LecturerId"]');

        const payload = {
            Title:       form.Title.value,
            Description: form.Description.value,
            MajorId:     parseInt(form.MajorId.value),
            LecturerId:  lecSelect ? (lecSelect.value || null) : null,
            Level:       form.Level.value,
            MaxStudents: parseInt(form.MaxStudents.value),
            Deadline:    form.Deadline.value || null
        };

        try {
            const res    = await fetch(CREATE_URL, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getToken()
                },
                body: JSON.stringify(payload)
            });
            const result = await res.json();

            if (result.success) {
                pageToast(result.message || 'Tạo đề tài thành công!');
                closeCreateModal();
                setTimeout(() => location.reload(), 1200);
            } else {
                pageToast(result.message || 'Tạo đề tài thất bại!', 'error');
            }
        } catch {
            pageToast('Có lỗi xảy ra!', 'error');
        } finally {
            submitBtn.disabled  = false;
            submitBtn.innerHTML = '<i class="fa-solid fa-plus"></i> Tạo đề tài';
        }
    }

    async function submitEditTopic(e) {
        e.preventDefault();

        const form = document.getElementById('editTopicForm');
        const submitBtn = document.getElementById('submitEditBtn');
        const formData = new FormData(form);

        if (!form.IsApproved.checked) formData.append('IsApproved', 'false');
        if (!form.IsRegistrationOpen.checked) formData.append('IsRegistrationOpen', 'false');

        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Đang lưu...';

        try {
            const res = await fetch(EDIT_URL, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': getToken(),
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });
            const result = await res.json();

            if (result.success) {
                pageToast(result.message || 'Đã cập nhật đề tài!');
                closeEditModal();
                setTimeout(() => location.reload(), 900);
            } else {
                pageToast(result.message || 'Cập nhật đề tài thất bại!', 'error');
            }
        } catch {
            pageToast('Có lỗi xảy ra khi cập nhật đề tài!', 'error');
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="fa-solid fa-floppy-disk"></i> Lưu thay đổi';
        }
    }

    // ── DELETE CONFIRM ──
    function confirmDelete(event, form) {
        event.preventDefault();
        Swal.fire({
            title: 'Xóa đề tài?',
            text:  'Dữ liệu sẽ không thể khôi phục!',
            icon:  'warning',
            showCancelButton:    true,
            confirmButtonColor:  '#dc2626',
            cancelButtonColor:   '#64748b',
            confirmButtonText:   'Xóa',
            cancelButtonText:    'Hủy',
            reverseButtons:      true
        }).then(r => { if (r.isConfirmed) form.submit(); });
    }

    // ── SELECT ALL ──
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-edit-topic]').forEach(button => {
            button.addEventListener('click', () => openEditModal(button));
        });

        document.querySelectorAll('[data-confirm-copy]').forEach(button => {
            button.closest('form')?.addEventListener('submit', async event => {
                event.preventDefault();
                const result = await Swal.fire({
                    title: 'Sao chép đề tài?',
                    text: button.dataset.confirmCopy || 'Sao chép đề tài này sang đợt hiện tại?',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonText: 'Sao chép',
                    cancelButtonText: 'Hủy',
                    confirmButtonColor: '#16a34a',
                    reverseButtons: true
                });

                if (result.isConfirmed) {
                    event.target.submit();
                }
            });
        });

        const selectAll  = document.getElementById('selectAllPending');
        if (!selectAll) return;

        const checkboxes = document.querySelectorAll('.pending-checkbox');

        selectAll.addEventListener('change', function () {
            checkboxes.forEach(cb => cb.checked = this.checked);
        });

        checkboxes.forEach(cb => {
            cb.addEventListener('change', () => {
                const checked = document.querySelectorAll('.pending-checkbox:checked').length;
                selectAll.checked = checked === checkboxes.length;
                selectAll.indeterminate = checked > 0 && checked < checkboxes.length;
            });
        });
    });

    function getSelectedIds() {
        return [...document.querySelectorAll('.pending-checkbox:checked')].map(x => parseInt(x.value));
    }

    // ── BULK APPROVE ──
    async function bulkApprove() {
        const ids = getSelectedIds();
        if (!ids.length) { pageToast('Vui lòng chọn đề tài', 'error'); return; }

        const r = await Swal.fire({
            title: 'Duyệt hàng loạt?', text: `Duyệt ${ids.length} đề tài đã chọn`,
            icon: 'question', showCancelButton: true,
            confirmButtonText: 'Duyệt', cancelButtonText: 'Hủy',
            confirmButtonColor: '#16a34a'
        });

        if (!r.isConfirmed) return;

        try {
            const res  = await fetch('/Admin/BulkApproveTopics', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getToken() },
                body: JSON.stringify(ids)
            });
            const data = await res.json();
            data.success ? (pageToast('Duyệt thành công!'), setTimeout(() => location.reload(), 1000))
                         : pageToast(data.message || 'Lỗi!', 'error');
        } catch { pageToast('Có lỗi xảy ra!', 'error'); }
    }

    // ── BULK REJECT ──
    async function bulkReject() {
        const ids = getSelectedIds();
        if (!ids.length) { pageToast('Vui lòng chọn đề tài', 'error'); return; }

        const r = await Swal.fire({
            title: 'Từ chối hàng loạt?', text: `Từ chối ${ids.length} đề tài đã chọn`,
            icon: 'warning', showCancelButton: true,
            confirmButtonText: 'Từ chối', cancelButtonText: 'Hủy',
            confirmButtonColor: '#dc2626'
        });

        if (!r.isConfirmed) return;

        try {
            const res  = await fetch('/Admin/BulkRejectTopics', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getToken() },
                body: JSON.stringify(ids)
            });
            const data = await res.json();
            data.success ? (pageToast('Từ chối thành công!'), setTimeout(() => location.reload(), 1000))
                         : pageToast(data.message || 'Lỗi!', 'error');
        } catch { pageToast('Có lỗi xảy ra!', 'error'); }
    }

    // Fix: TempData qua data attribute thay vì inline script — tránh lỗi ký tự đặc biệt
    document.addEventListener('DOMContentLoaded', () => {
        const el = document.getElementById('tempdata-msg');
        if (!el) return;
        const msg  = el.dataset.msg;
        const type = el.dataset.type;
        if (msg) pageToast(msg, type);
    });
