(function () {
    const approveModal = document.getElementById("approveModal");
    const rejectModal = document.getElementById("rejectModal");
    const approveContainer = document.getElementById("approveIdsContainer");
    const rejectContainer = document.getElementById("modalIdsContainer");
    const rejectTopicName = document.getElementById("rejectTopicName");

    function setBodyModalState(isOpen) {
        document.body.style.overflow = isOpen ? "hidden" : "";
    }

    function fillRegistrationIds(button, container) {
        if (!button || !container) {
            return;
        }

        const row = button.closest("tr");
        container.innerHTML = "";

        if (!row) {
            return;
        }

        row.querySelectorAll(".reg-id").forEach((input) => {
            const hidden = document.createElement("input");
            hidden.type = "hidden";
            hidden.name = "ids";
            hidden.value = input.value;
            container.appendChild(hidden);
        });
    }

    function openModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.add("open");
        setBodyModalState(true);
    }

    function closeModal(modal) {
        if (!modal) {
            return;
        }

        modal.classList.remove("open");

        if (!document.querySelector(".modal-overlay.open")) {
            setBodyModalState(false);
        }
    }

    document.querySelectorAll("[data-approve-trigger]").forEach((button) => {
        button.addEventListener("click", () => {
            fillRegistrationIds(button, approveContainer);
            openModal(approveModal);
        });
    });

    document.querySelectorAll("[data-reject-trigger]").forEach((button) => {
        button.addEventListener("click", () => {
            fillRegistrationIds(button, rejectContainer);

            if (rejectTopicName) {
                rejectTopicName.textContent = button.dataset.topic || "";
            }

            openModal(rejectModal);
        });
    });

    document.querySelectorAll("[data-close-modal]").forEach((button) => {
        button.addEventListener("click", () => {
            const target = button.dataset.closeModal === "approve" ? approveModal : rejectModal;
            closeModal(target);
        });
    });

    document.querySelectorAll("[data-modal-overlay]").forEach((overlay) => {
        overlay.addEventListener("click", (event) => {
            if (event.target === overlay) {
                closeModal(overlay);
            }
        });
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeModal(approveModal);
            closeModal(rejectModal);
        }
    });
})();
