(() => {
    const approveModal = document.getElementById('approveModal');
    const rejectModal = document.getElementById('rejectModal');
    const approveSubId = document.getElementById('approveSubId');
    const approveStudentName = document.getElementById('approveStudentName');
    const approveWarnBox = document.getElementById('approveWarnBox');
    const approveWarnText = document.getElementById('approveWarnText');
    const rejectForm = document.getElementById('rejectForm');
    const rejectSubId = document.getElementById('rejectSubId');
    const rejectStudentName = document.getElementById('rejectStudentName');
    const rejectComment = document.getElementById('rejectComment');
    const rejectError = document.getElementById('rejectError');
    const charCount = document.getElementById('charCount');

    const toggleBodyScroll = () => {
        const hasOpenModal = approveModal?.classList.contains('open') || rejectModal?.classList.contains('open');
        document.body.style.overflow = hasOpenModal ? 'hidden' : '';
    };

    const closeApproveModal = () => {
        approveModal?.classList.remove('open');
        toggleBodyScroll();
    };

    const closeRejectModal = () => {
        rejectModal?.classList.remove('open');
        toggleBodyScroll();
    };

    const updateCharCount = () => {
        if (!rejectComment || !charCount) return;

        const len = rejectComment.value.length;
        charCount.textContent = `${len} / 500`;
        charCount.classList.toggle('warn', len > 400);

        if (len >= 15) {
            rejectComment.classList.remove('invalid');
            rejectError?.classList.remove('show');
        }
    };

    const openApproveModal = (button) => {
        if (!approveModal || !approveSubId || !approveStudentName) return;

        const { submissionId, studentName, deadlineStatus, deadlineTime } = button.dataset;
        approveSubId.value = submissionId || '';
        approveStudentName.textContent = studentName ? `Sinh viên: ${studentName}` : '';

        if (deadlineStatus === 'near' && deadlineTime && approveWarnBox && approveWarnText) {
            approveWarnText.textContent =
                `Hạn duyệt sắp kết thúc lúc ${deadlineTime}. Hãy hoàn tất duyệt sớm để kịp tổng hợp lên Admin.`;
            approveWarnBox.classList.remove('is-hidden');
        } else {
            approveWarnBox?.classList.add('is-hidden');
        }

        approveModal.classList.add('open');
        toggleBodyScroll();
    };

    const openRejectModal = (button) => {
        if (!rejectModal || !rejectSubId || !rejectStudentName || !rejectComment || !charCount) return;

        const { submissionId, studentName } = button.dataset;
        rejectSubId.value = submissionId || '';
        rejectStudentName.textContent = studentName || '';
        rejectComment.value = '';
        rejectComment.classList.remove('invalid');
        rejectError?.classList.remove('show');
        charCount.textContent = '0 / 500';
        charCount.classList.remove('warn');

        rejectModal.classList.add('open');
        toggleBodyScroll();
        window.setTimeout(() => rejectComment.focus(), 200);
    };

    const validateReject = () => {
        if (!rejectComment || !rejectError) return true;

        const val = rejectComment.value.trim();
        if (val.length < 15) {
            rejectComment.classList.add('invalid');
            rejectError.classList.add('show');
            rejectComment.focus();
            return false;
        }

        rejectComment.classList.remove('invalid');
        rejectError.classList.remove('show');
        return true;
    };

    document.querySelectorAll('.js-progress-fill').forEach((el) => {
        const progress = Number.parseInt(el.dataset.progress || '0', 10);
        el.style.setProperty('--progress', `${Math.min(100, Math.max(0, progress))}%`);
    });

    document.addEventListener('click', (event) => {
        const toggle = event.target.closest('.js-toggle-section');
        if (toggle) {
            event.stopPropagation();
            const { bodyId, buttonId } = toggle.dataset;
            document.getElementById(bodyId)?.classList.toggle('open');
            document.getElementById(buttonId)?.classList.toggle('open');
            return;
        }

        const approveButton = event.target.closest('.js-open-approve');
        if (approveButton) {
            openApproveModal(approveButton);
            return;
        }

        const rejectButton = event.target.closest('.js-open-reject');
        if (rejectButton) {
            openRejectModal(rejectButton);
            return;
        }

        const reasonChip = event.target.closest('.reason-chip[data-reason]');
        if (reasonChip && rejectComment) {
            rejectComment.value = reasonChip.dataset.reason || '';
            updateCharCount();
            rejectComment.classList.remove('invalid');
            rejectError?.classList.remove('show');
            rejectComment.focus();
            return;
        }

        if (event.target.matches('.js-close-approve')) {
            closeApproveModal();
            return;
        }

        if (event.target.matches('.js-close-reject')) {
            closeRejectModal();
            return;
        }

        if (event.target.classList.contains('js-modal-overlay')) {
            closeApproveModal();
            closeRejectModal();
        }
    });

    rejectComment?.addEventListener('input', updateCharCount);

    rejectForm?.addEventListener('submit', (event) => {
        if (!validateReject()) {
            event.preventDefault();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') {
            closeApproveModal();
            closeRejectModal();
        }
    });
})();
