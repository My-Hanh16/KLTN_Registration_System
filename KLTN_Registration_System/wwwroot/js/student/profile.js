(function () {
    const newPasswordInput = document.getElementById("newPwd");
    const confirmPasswordInput = document.getElementById("confirmPwd");
    const matchMessage = document.getElementById("matchMsg");
    const passwordForm = confirmPasswordInput?.closest("form");

    function checkPasswordMatch() {
        if (!newPasswordInput || !confirmPasswordInput || !matchMessage) {
            return;
        }

        const password = newPasswordInput.value;
        const confirmPassword = confirmPasswordInput.value;

        if (!confirmPassword) {
            matchMessage.textContent = "";
            matchMessage.classList.remove("password-match-success", "password-match-danger");
            return;
        }

        const isMatched = password === confirmPassword;
        matchMessage.textContent = isMatched ? "✓ Mật khẩu khớp" : "✗ Mật khẩu không khớp";
        matchMessage.classList.toggle("password-match-success", isMatched);
        matchMessage.classList.toggle("password-match-danger", !isMatched);
    }

    document.querySelectorAll("[data-toggle-password]").forEach((button) => {
        button.addEventListener("click", () => {
            const inputId = button.dataset.target;
            const input = document.getElementById(inputId);
            const icon = button.querySelector("i");

            if (!input || !icon) {
                return;
            }

            const shouldShow = input.type === "password";
            input.type = shouldShow ? "text" : "password";
            icon.classList.toggle("fa-eye", !shouldShow);
            icon.classList.toggle("fa-eye-slash", shouldShow);
        });
    });

    if (newPasswordInput) {
        newPasswordInput.addEventListener("input", checkPasswordMatch);
    }

    if (confirmPasswordInput) {
        confirmPasswordInput.addEventListener("input", checkPasswordMatch);
    }

    passwordForm?.addEventListener("submit", (event) => {
        if (!newPasswordInput || !confirmPasswordInput) {
            return;
        }

        if (newPasswordInput.value !== confirmPasswordInput.value) {
            event.preventDefault();
            checkPasswordMatch();
            confirmPasswordInput.focus();
        }
    });
})();
