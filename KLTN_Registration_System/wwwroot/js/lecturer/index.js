(function () {
    function getModal() {
        return document.getElementById('createModal');
    }

    function showToast(message, isSuccess = true) {
        const toast = document.getElementById('toast');
        const text = document.getElementById('toastMessage');

        if (!toast || !text) {
            return;
        }

        text.innerText = message;
        toast.classList.toggle('bg-green-500', isSuccess);
        toast.classList.toggle('bg-red-500', !isSuccess);
        toast.classList.add('toast-show');

        window.clearTimeout(window.lecturerToastTimer);
        window.lecturerToastTimer = window.setTimeout(function () {
            toast.classList.remove('toast-show');
        }, 3000);
    }

    function openCreateModal() {
        const modal = getModal();
        const noMajorAssigned = modal?.dataset.noMajorAssigned === 'true';

        if (noMajorAssigned) {
            showToast('Bạn chưa được Admin phân công chuyên ngành nên chưa thể tạo đề tài.', false);
            return;
        }

        modal?.classList.add('is-open');
    }

    function closeCreateModal() {
        getModal()?.classList.remove('is-open');
    }

    async function createTopic() {
        const titleInput = document.getElementById('topicTitle');
        const title = titleInput?.value || '';

        if (!title.trim()) {
            showToast('Vui lòng nhập tên đề tài', false);
            return;
        }

        const majorElement = document.getElementById('majorId');
        const majorId = parseInt(majorElement?.value || '');

        if (!majorId) {
            showToast('Bạn chưa được phân công chuyên ngành để tạo đề tài.', false);
            return;
        }

        const deadlineElement = document.getElementById('deadline');
        const topic = {
            title: title,
            description: document.getElementById('topicDescription')?.value || '',
            majorId: majorId,
            deadline: deadlineElement && deadlineElement.value ? deadlineElement.value : undefined,
            maxStudents: parseInt(document.getElementById('maxMembers')?.value || '1'),
            category: document.getElementById('topicType')?.value || ''
        };

        try {
            const response = await fetch('/Lecturer/Create', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('#createModal input[name="__RequestVerificationToken"]')?.value ?? ''
                },
                body: JSON.stringify(topic)
            });

            const result = await response.json();

            if (result.success) {
                showToast(result.message, true);
                closeCreateModal();
                location.reload();
            } else {
                showToast(result.message || 'Tạo đề tài thất bại', false);
            }
        } catch (err) {
            console.error(err);
            showToast('Có lỗi xảy ra', false);
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.topic-fill[data-fill-percent]').forEach(function (bar) {
            const percent = Math.max(0, Math.min(100, Number(bar.dataset.fillPercent || 0)));
            bar.style.width = percent + '%';
        });

        document.querySelectorAll('.topic-donut[data-app-rate]').forEach(function (donut) {
            const appRate = Math.max(0, Math.min(100, Number(donut.dataset.appRate || 0)));
            donut.style.background = `conic-gradient(#475569 0% ${appRate}%, #0284c7 ${appRate}% 100%)`;
        });

        document.querySelector('.js-open-create-modal')?.addEventListener('click', openCreateModal);
        document.querySelectorAll('.js-close-create-modal').forEach(function (button) {
            button.addEventListener('click', closeCreateModal);
        });
        document.querySelector('.js-create-topic')?.addEventListener('click', createTopic);

        document.querySelectorAll('.js-confirm-submit[data-confirm-message]').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (!window.confirm(form.dataset.confirmMessage || 'Bạn có chắc chắn muốn tiếp tục?')) {
                    event.preventDefault();
                }
            });
        });
    });
})();
