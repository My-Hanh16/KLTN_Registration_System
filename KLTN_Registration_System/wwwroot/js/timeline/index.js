function openModal(id) {
            const modal = document.getElementById(id);
            if (!modal) return;
            modal.classList.add('open');
            document.body.style.overflow = 'hidden';
        }

        function closeModal(id) {
            const modal = document.getElementById(id);
            if (!modal) return;
            modal.classList.remove('open');
            document.body.style.overflow = '';
        }

        function openEdit(id, title, desc, date, subDl, revDl, isActive, allowSub, type, subType) {
            document.getElementById('editId').value        = id;
            document.getElementById('editTitle').value     = title;
            document.getElementById('editDesc').value      = desc;
            document.getElementById('editDate').value      = date;
            document.getElementById('editSubDl').value     = subDl;
            document.getElementById('editRevDl').value     = revDl;
            document.getElementById('editActive').checked  = isActive;
            document.getElementById('editAllowSub').checked = allowSub || !!subDl;
            document.getElementById('editSubType').value   = subType;

            // Set select type
            const sel = document.getElementById('editType');
            for (let i = 0; i < sel.options.length; i++) {
                if (sel.options[i].value === type) { sel.selectedIndex = i; break; }
            }

            openModal('modalEdit');
        }

        function confirmDelete(event, title) {
            event.preventDefault();
            const form = event.target;
            Swal.fire({
                title: 'Xóa mốc thời gian?',
                text: title,
                icon: 'warning',
                showCancelButton:   true,
                confirmButtonColor: '#dc2626',
                cancelButtonColor:  '#64748b',
                confirmButtonText:  'Xóa',
                cancelButtonText:   'Hủy',
                reverseButtons:     true
            }).then(r => { if (r.isConfirmed) form.submit(); });
            return false;
        }

        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') {
                closeModal('modalAdd');
                closeModal('modalEdit');
            }
        });

        document.querySelectorAll('.js-open-modal').forEach(button => {
            button.addEventListener('click', () => {
                openModal(button.dataset.modalTarget);
            });
        });

        document.querySelectorAll('.js-close-modal').forEach(button => {
            button.addEventListener('click', () => {
                closeModal(button.dataset.modalTarget);
            });
        });

        document.querySelectorAll('.js-modal-overlay').forEach(overlay => {
            overlay.addEventListener('click', event => {
                if (event.target === overlay) {
                    closeModal(overlay.id);
                }
            });
        });

        document.querySelectorAll('.edit-timeline-btn').forEach(button => {
            button.addEventListener('click', () => {
                openEdit(
                    button.dataset.id,
                    button.dataset.title || '',
                    button.dataset.desc || '',
                    button.dataset.date || '',
                    button.dataset.subDl || '',
                    button.dataset.revDl || '',
                    button.dataset.active === 'true',
                    button.dataset.allowSub === 'true',
                    button.dataset.type || '',
                    button.dataset.subType || ''
                );
            });
        });

        document.querySelectorAll('.delete-timeline-form').forEach(form => {
            form.addEventListener('submit', event => {
                confirmDelete(event, form.dataset.title || 'mốc thời gian này');
            });
        });

        document.querySelectorAll('form').forEach(form => {
            const subDl = form.querySelector('[name="submissionDeadline"]');
            const allowSub = form.querySelector('[name="allowSubmission"]');
            if (!subDl || !allowSub) return;

            subDl.addEventListener('change', () => {
                if (subDl.value) allowSub.checked = true;
            });
        });

        // Validate: ReviewDeadline phải sau SubmissionDeadline
        document.querySelectorAll('form').forEach(form => {
            form.addEventListener('submit', function(e) {
                const openAt = this.querySelector('[name="date"]');
                const subDl = this.querySelector('[name="submissionDeadline"]');
                const revDl = this.querySelector('[name="reviewDeadline"]');
                const allowSub = this.querySelector('[name="allowSubmission"]');
                const requiresSubmission = allowSub && allowSub.checked;
                if (!subDl || !revDl) return;
                if (requiresSubmission && openAt && openAt.value && subDl.value && openAt.value >= subDl.value) {
                    e.preventDefault();
                    Swal.fire({
                        icon: 'error',
                        title: 'Thời gian nộp không hợp lệ',
                        text: 'Thời điểm mở nộp phải trước hạn cuối sinh viên nộp bài.',
                        confirmButtonColor: '#4f46e5'
                    });
                    return;
                }
                if (requiresSubmission && subDl.value && revDl.value && revDl.value <= subDl.value) {
                    e.preventDefault();
                    Swal.fire({
                        icon: 'error',
                        title: 'Deadline không hợp lệ',
                        text: 'Hạn GV duyệt phải sau Hạn SV nộp.',
                        confirmButtonColor: '#4f46e5'
                    });
                }
            });
        });
