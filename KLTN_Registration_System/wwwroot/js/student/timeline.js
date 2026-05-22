(function () {
    document.querySelectorAll("[data-toggle-timeline]").forEach((button) => {
        button.addEventListener("click", () => {
            const targetId = button.dataset.target;
            const content = document.getElementById(targetId);
            const icon = button.querySelector("i");
            const text = button.querySelector(".btn-text");

            if (!content || !icon || !text) {
                return;
            }

            const isHidden = content.classList.contains("tl-collapse-hidden");

            content.classList.toggle("tl-collapse-hidden", !isHidden);
            content.classList.toggle("tl-collapse-visible", isHidden);
            button.classList.toggle("active", isHidden);
            icon.classList.toggle("bi-chevron-down", !isHidden);
            icon.classList.toggle("bi-chevron-up", isHidden);
            text.textContent = isHidden ? "Thu gọn" : "Xem chi tiết";
        });
    });
})();
