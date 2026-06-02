(function () {
    document.addEventListener('DOMContentLoaded', function () {
        const searchBox = document.getElementById('chatSearch');
        const noSearchResult = document.getElementById('noSearchResult');

        if (!searchBox) {
            return;
        }

        searchBox.addEventListener('input', function () {
            const keyword = searchBox.value.toLowerCase().trim();
            let visibleCount = 0;

            document.querySelectorAll('.topic-item').forEach(function (card) {
                const matched = card.innerText.toLowerCase().includes(keyword);
                card.classList.toggle('hidden-by-search', !matched);
                if (matched) {
                    visibleCount++;
                }
            });

            noSearchResult?.classList.toggle('hidden-by-search', visibleCount > 0);
        });
    });
})();
