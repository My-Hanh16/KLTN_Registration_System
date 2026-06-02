let pendingRegisterForm = null;

        function openRegisterConfirm(form) {
            pendingRegisterForm = form;
            const modal = document.getElementById('registerConfirm');
            const topicBox = document.getElementById('registerConfirmTopic');
            topicBox.textContent = form.dataset.topicTitle || 'Đề tài đã chọn';
            modal.classList.add('open');
            modal.setAttribute('aria-hidden', 'false');
        }

        function closeRegisterConfirm() {
            const modal = document.getElementById('registerConfirm');
            modal.classList.remove('open');
            modal.setAttribute('aria-hidden', 'true');
            pendingRegisterForm = null;
        }

        document.querySelectorAll('.topic-register-form').forEach(form => {
            form.addEventListener('submit', event => {
                if (form.dataset.confirmed === 'true') {
                    return;
                }

                event.preventDefault();
                openRegisterConfirm(form);
            });
        });

        document.getElementById('cancelRegisterConfirm')?.addEventListener('click', closeRegisterConfirm);

        document.getElementById('submitRegisterConfirm')?.addEventListener('click', () => {
            if (!pendingRegisterForm) return;
            pendingRegisterForm.dataset.confirmed = 'true';
            pendingRegisterForm.submit();
        });

        document.getElementById('registerConfirm')?.addEventListener('click', event => {
            if (event.target.id === 'registerConfirm') {
                closeRegisterConfirm();
            }
        });

        document.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                closeRegisterConfirm();
                closeGroupModal();
            }
        });

        const groupModal = document.getElementById('groupRegisterModal');
        const groupForm = document.getElementById('groupRegisterForm');
        const groupTopicId = document.getElementById('groupTopicId');
        const groupTitle = document.getElementById('groupModalTitle');
        const groupRuleMax = document.getElementById('groupRuleMax');
        const groupRuleSlots = document.getElementById('groupRuleSlots');
        const groupMemberFields = document.getElementById('groupMemberFields');
        const groupError = document.getElementById('groupClientError');

        function openGroupModal(button) {
            const topicId = button.dataset.topicId;
            const title = button.dataset.topicTitle || 'Đăng ký nhóm';
            const maxStudents = Number(button.dataset.maxStudents || '2');
            const currentMembers = Number(button.dataset.currentMembers || '0');
            const remainingSlots = Number(button.dataset.remainingSlots || '0');
            const memberInputCount = Math.max(1, Math.min(maxStudents - 1, remainingSlots - 1));

            groupTopicId.value = topicId;
            groupTitle.textContent = title;
            groupRuleMax.innerHTML = `Đề tài nhóm cần tối thiểu <b>2</b> sinh viên và tối đa <b>${maxStudents}</b> sinh viên.`;
            groupRuleSlots.innerHTML = `Đã có <b>${currentMembers}</b> sinh viên chờ/đã duyệt, còn <b>${remainingSlots}</b> chỗ cho nhóm mới.`;
            groupMemberFields.innerHTML = '';
            groupError.classList.remove('show');

            for (let i = 1; i <= memberInputCount; i++) {
                const field = document.createElement('div');
                field.className = 'group-member-field';
                field.innerHTML = `
                    <label>Thành viên ${i}${i === 1 ? ' *' : ''}</label>
                    <input type="text"
                           name="memberEmails"
                           placeholder="Nhập Email hoặc MSSV thành viên..."
                           ${i === 1 ? 'required' : ''} />
                `;
                groupMemberFields.appendChild(field);
            }

            groupModal.classList.add('open');
            groupModal.setAttribute('aria-hidden', 'false');
            document.body.classList.add('group-modal-open');
            groupMemberFields.querySelector('input')?.focus();
        }

        function closeGroupModal() {
            if (!groupModal) return;
            groupModal.classList.remove('open');
            groupModal.setAttribute('aria-hidden', 'true');
            document.body.classList.remove('group-modal-open');
            groupForm?.reset();
            groupMemberFields.innerHTML = '';
            groupError.classList.remove('show');
        }

        document.querySelectorAll('.js-open-group-register').forEach(button => {
            button.addEventListener('click', () => openGroupModal(button));
        });

        document.querySelectorAll('.js-close-group-modal').forEach(button => {
            button.addEventListener('click', closeGroupModal);
        });

        groupModal?.addEventListener('click', event => {
            if (event.target === groupModal) {
                closeGroupModal();
            }
        });

        groupForm?.addEventListener('submit', event => {
            const submitButton = groupForm.querySelector('button[type="submit"]');
            const members = Array.from(groupForm.querySelectorAll('input[name="memberEmails"]'))
                .map(input => input.value.trim())
                .filter(Boolean);

            if (members.length < 1) {
                event.preventDefault();
                groupError.classList.add('show');
                groupMemberFields.querySelector('input')?.focus();
            } else {
                groupError.classList.remove('show');
                if (submitButton) {
                    submitButton.disabled = true;
                    submitButton.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang gửi đăng ký...';
                }
            }
        });

        function submitLevel(val) {
            const input = document.getElementById('levelInput');
            input.value = (input.value === val) ? "" : val;
            document.getElementById('filterForm').submit();
        }

        function submitType(val) {
            const input = document.getElementById('typeInput');

            input.value = (input.value === val) ? "" : val;

            document.getElementById('filterForm').submit();
        }

        document.querySelectorAll('[data-level-filter]').forEach(button => {
            button.addEventListener('click', () => submitLevel(button.dataset.levelFilter));
        });

        document.querySelectorAll('[data-type-filter]').forEach(button => {
            button.addEventListener('click', () => submitType(button.dataset.typeFilter));
        });
