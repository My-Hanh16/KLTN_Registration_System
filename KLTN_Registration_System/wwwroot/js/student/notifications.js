(function () {
    const filterRow = document.getElementById("filterRow");
    const items = Array.from(document.querySelectorAll("#notifList .nc"));
    const empty = document.getElementById("filteredEmpty");
    const message = document.getElementById("filteredMsg");

    if (!filterRow || !empty || !message) {
        return;
    }

    const messages = {
        all: "Bạn chưa có thông báo nào.",
        unread: "Không có thông báo chưa đọc.",
        topic: "Không có thông báo đề tài.",
        system: "Không có thông báo hệ thống.",
    };

    filterRow.addEventListener("click", (event) => {
        const button = event.target.closest(".ftab[data-filter]");
        if (!button) {
            return;
        }

        const filter = button.dataset.filter || "all";

        filterRow
            .querySelectorAll(".ftab")
            .forEach((tab) => tab.classList.remove("active"));

        button.classList.add("active");

        let shown = 0;

        items.forEach((item) => {
            const match =
                filter === "all"
                    ? true
                    : filter === "unread"
                        ? item.dataset.read === "0"
                        : filter === "topic"
                            ? item.dataset.cat === "topic"
                            : filter === "system"
                                ? item.dataset.cat === "system"
                                : true;

            item.hidden = !match;

            if (match) {
                shown++;
            }
        });

        message.textContent = messages[filter] || "Không có thông báo nào.";
        empty.hidden = shown !== 0;
    });
})();
