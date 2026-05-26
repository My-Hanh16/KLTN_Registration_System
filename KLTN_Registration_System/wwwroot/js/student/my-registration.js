(function () {
    document.querySelectorAll(".progress-fill[data-progress]").forEach((bar) => {
        const progress = Number.parseInt(bar.dataset.progress || "0", 10);
        const normalizedProgress = Number.isNaN(progress)
            ? 0
            : Math.min(Math.max(progress, 0), 100);

        bar.style.width = normalizedProgress + "%";
    });

    const cancelModal = document.getElementById("cancelConfirmModal");
    const cancelTopic = document.getElementById("cancelConfirmTopic");
    const closeCancelButton = document.getElementById("cancelConfirmClose");
    const submitCancelButton = document.getElementById("cancelConfirmSubmit");
    let pendingCancelForm = null;

    function openCancelModal(form) {
        if (!cancelModal) {
            form.dataset.confirmed = "true";
            form.submit();
            return;
        }

        pendingCancelForm = form;
        const topicTitle = form.dataset.topicTitle || "đề tài này";

        if (cancelTopic) {
            cancelTopic.textContent = topicTitle;
        }

        cancelModal.classList.add("is-open");
        cancelModal.setAttribute("aria-hidden", "false");
        submitCancelButton?.focus();
    }

    function closeCancelModal() {
        if (!cancelModal) {
            return;
        }

        pendingCancelForm = null;
        cancelModal.classList.remove("is-open");
        cancelModal.setAttribute("aria-hidden", "true");
    }

    document.querySelectorAll(".cancel-registration-form").forEach((form) => {
        form.addEventListener("submit", (event) => {
            if (form.dataset.confirmed === "true") {
                return;
            }

            event.preventDefault();
            openCancelModal(form);
        });
    });

    closeCancelButton?.addEventListener("click", closeCancelModal);

    submitCancelButton?.addEventListener("click", () => {
        if (!pendingCancelForm) {
            closeCancelModal();
            return;
        }

        pendingCancelForm.dataset.confirmed = "true";
        pendingCancelForm.submit();
    });

    cancelModal?.addEventListener("click", (event) => {
        if (event.target === cancelModal) {
            closeCancelModal();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && cancelModal?.classList.contains("is-open")) {
            closeCancelModal();
        }
    });
})();
