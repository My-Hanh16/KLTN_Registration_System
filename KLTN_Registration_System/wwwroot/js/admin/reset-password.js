(function () {
    function setPasswordMode(isRandom) {
        const field = document.getElementById('customPasswordField');
        const input = document.getElementById('newPasswordInput');
        const labelRandom = document.getElementById('labelRandom');
        const labelCustom = document.getElementById('labelCustom');

        if (!field || !input || !labelRandom || !labelCustom) return;

        field.classList.toggle('is-hidden', isRandom);
        input.required = !isRandom;

        labelRandom.classList.toggle('mode-card--active', isRandom);
        labelCustom.classList.toggle('mode-card--active', !isRandom);
    }

    function toggleVisibility() {
        const input = document.getElementById('newPasswordInput');
        const icon = document.getElementById('eyeIcon');
        if (!input || !icon) return;

        const showPassword = input.type === 'password';
        input.type = showPassword ? 'text' : 'password';
        icon.textContent = showPassword ? 'visibility_off' : 'visibility';
    }

    document.addEventListener('DOMContentLoaded', function () {
        const radioRandom = document.getElementById('radioRandom');
        const radioCustom = document.getElementById('radioCustom');
        const visibilityButton = document.getElementById('togglePasswordVisibility');

        radioRandom?.addEventListener('change', () => setPasswordMode(true));
        radioCustom?.addEventListener('change', () => setPasswordMode(false));
        visibilityButton?.addEventListener('click', toggleVisibility);

        setPasswordMode(radioRandom?.checked !== false);
    });
})();
