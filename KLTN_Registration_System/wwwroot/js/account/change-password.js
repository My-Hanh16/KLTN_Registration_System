(function () {
    const form = document.getElementById("changePwdForm");
    const newPassword = document.getElementById("newPassword");
    const confirmPassword = document.getElementById("confirmPassword");
    const submitButton = document.getElementById("submitBtn");

    if (!form || !newPassword || !confirmPassword || !submitButton) {
        return;
    }

    document.querySelectorAll("[data-toggle-password]").forEach((button) => {
        button.addEventListener("click", () => {
            const input = document.getElementById(button.dataset.target);
            const icon = button.querySelector("i");

            if (!input || !icon) {
                return;
            }

            const isHidden = input.type === "password";
            input.type = isHidden ? "text" : "password";
            icon.className = isHidden ? "fa-regular fa-eye-slash" : "fa-regular fa-eye";
        });
    });

    newPassword.addEventListener("input", () => {
        checkStrength(newPassword.value);
        validateConfirm();
    });

    confirmPassword.addEventListener("input", validateConfirm);

    form.addEventListener("submit", (event) => {
        if (newPassword.value !== confirmPassword.value) {
            event.preventDefault();
            validateConfirm();
            return;
        }

        submitButton.disabled = true;
        submitButton.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Đang cập nhật...';
    });

    function checkStrength(value) {
        const wrap = document.getElementById("strengthWrap");
        const label = document.getElementById("strengthLabel");
        const bars = [1, 2, 3, 4].map((i) => document.getElementById("bar" + i));

        if (!wrap || !label || bars.some((bar) => !bar)) {
            return;
        }

        if (!value) {
            wrap.hidden = true;
            return;
        }

        wrap.hidden = false;

        let score = 0;
        if (value.length >= 8) score++;
        if (value.length >= 10) score++;
        if (/[A-Z]/.test(value) && /[0-9]/.test(value)) score++;
        if (/[^A-Za-z0-9]/.test(value)) score++;

        const levels = [
            { cls: "weak", text: "Yếu", fill: 1 },
            { cls: "weak", text: "Yếu", fill: 1 },
            { cls: "medium", text: "Trung bình", fill: 2 },
            { cls: "medium", text: "Khá mạnh", fill: 3 },
            { cls: "strong", text: "Mạnh", fill: 4 },
        ];

        const level = levels[score];
        bars.forEach((bar, index) => {
            bar.className = "strength-bar " + (index < level.fill ? level.cls : "");
        });

        label.className = "strength-label " + level.cls;
        label.textContent = level.text;

        setCheck("chk-len", value.length >= 8);
        setCheck("chk-num", /[0-9]/.test(value));
        setCheck("chk-upper", /[A-Z]/.test(value));
        setCheck("chk-special", /[^A-Za-z0-9]/.test(value));
    }

    function setCheck(id, pass) {
        const element = document.getElementById(id);
        const icon = element?.querySelector("i");

        if (!element || !icon) {
            return;
        }

        element.classList.toggle("pass", pass);
        icon.className = pass ? "fa-solid fa-circle-check" : "fa-regular fa-circle";
    }

    function validateConfirm() {
        const hint = document.getElementById("confirmHint");

        if (!hint) {
            return;
        }

        if (!confirmPassword.value) {
            hint.hidden = true;
            confirmPassword.className = "form-control-custom";
            submitButton.disabled = false;
            return;
        }

        const match = newPassword.value === confirmPassword.value;
        hint.hidden = false;

        if (match) {
            confirmPassword.className = "form-control-custom is-ok";
            hint.className = "field-hint ok";
            hint.innerHTML = '<i class="fa-solid fa-circle-check"></i> Mật khẩu khớp';
        } else {
            confirmPassword.className = "form-control-custom is-error";
            hint.className = "field-hint error";
            hint.innerHTML = '<i class="fa-solid fa-circle-exclamation"></i> Mật khẩu không khớp';
        }

        submitButton.disabled = !match;
    }
})();
