(function () {
    document.querySelectorAll(".progress-fill[data-progress]").forEach((bar) => {
        const progress = Number.parseInt(bar.dataset.progress || "0", 10);
        const normalizedProgress = Number.isNaN(progress)
            ? 0
            : Math.min(Math.max(progress, 0), 100);

        bar.style.width = normalizedProgress + "%";
    });

    document.querySelectorAll(".cancel-registration-form").forEach((form) => {
        form.addEventListener("submit", (event) => {
            const confirmed = window.confirm("Bạn có chắc chắn muốn hủy đăng ký này?");

            if (!confirmed) {
                event.preventDefault();
            }
        });
    });
})();
