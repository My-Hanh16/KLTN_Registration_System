(function () {
    const passwordInput = document.getElementById("password");
    const toggleButton = document.getElementById("togglePasswordBtn");
    const eyeIcon = document.getElementById("eyeIcon");
    const loginForm = document.getElementById("loginForm");

    if (toggleButton && passwordInput && eyeIcon) {
        toggleButton.addEventListener("click", () => {
            const isHidden = passwordInput.type === "password";
            passwordInput.type = isHidden ? "text" : "password";
            eyeIcon.classList.toggle("fa-eye", !isHidden);
            eyeIcon.classList.toggle("fa-eye-slash", isHidden);
        });
    }

    if (loginForm) {
        loginForm.addEventListener("submit", () => {
            const username = document.getElementById("username")?.value.trim();
            const password = passwordInput?.value;

            if (!username || !password) {
                return;
            }

            const submitButton = document.getElementById("submitBtn");
            const spinner = document.getElementById("spinner");
            const submitText = document.getElementById("submitText");

            if (submitButton) {
                submitButton.disabled = true;
            }

            if (spinner) {
                spinner.style.display = "block";
            }

            if (submitText) {
                submitText.textContent = "Đang đăng nhập...";
            }
        });
    }
})();
