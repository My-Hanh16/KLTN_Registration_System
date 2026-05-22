(function () {
    function removeToast(id) {
        const element = document.getElementById(id);

        if (!element) {
            return;
        }

        element.classList.remove("show");
        setTimeout(() => element.remove(), 350);
    }

    document.addEventListener("DOMContentLoaded", () => {
        document.querySelectorAll(".toast-item").forEach((toast) => {
            requestAnimationFrame(() => toast.classList.add("show"));
            setTimeout(() => removeToast(toast.id), 4500);
        });

        document.querySelectorAll("[data-toast-close]").forEach((button) => {
            button.addEventListener("click", () => removeToast(button.dataset.toastClose));
        });
    });
})();
