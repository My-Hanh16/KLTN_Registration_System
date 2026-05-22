(function () {
    const form = document.getElementById("importExcelForm");
    const input = document.getElementById("excelInput");
    const chooseButton = document.getElementById("chooseExcelBtn");
    const fileName = document.getElementById("fileName");
    const submitButton = document.getElementById("submitBtn");
    const loadingText = document.getElementById("loadingText");

    if (!form || !input || !chooseButton || !fileName || !submitButton || !loadingText) {
        return;
    }

    chooseButton.addEventListener("click", () => {
        input.click();
    });

    input.addEventListener("change", () => {
        const file = input.files?.[0];

        if (!file) {
            fileName.textContent = "";
            submitButton.hidden = true;
            return;
        }

        fileName.textContent = "File đã chọn: " + file.name;
        submitButton.hidden = false;
    });

    form.addEventListener("submit", () => {
        loadingText.hidden = false;
        submitButton.disabled = true;
    });
})();
