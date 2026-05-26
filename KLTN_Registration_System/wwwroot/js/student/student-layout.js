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

        const menuButton = document.getElementById("studentMenuBtn");
        const closeSidebarElements = document.querySelectorAll("[data-close-sidebar]");

        menuButton?.addEventListener("click", () => {
            document.body.classList.toggle("sidebar-open");
        });

        closeSidebarElements.forEach((element) => {
            element.addEventListener("click", () => document.body.classList.remove("sidebar-open"));
        });

        document.querySelectorAll(".sidebar .menu-item").forEach((item) => {
            item.addEventListener("click", () => {
                if (window.innerWidth <= 992) {
                    document.body.classList.remove("sidebar-open");
                }
            });
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                document.body.classList.remove("sidebar-open");
            }
        });
    });
})();
