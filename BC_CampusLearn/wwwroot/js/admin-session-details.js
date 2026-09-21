(() => {
    const pageSize = 5;

    const reviewPanel = document.querySelector("#administrator-review-panel[data-open-on-error='true']");
    if (reviewPanel && window.bootstrap?.Offcanvas) {
        window.bootstrap.Offcanvas.getOrCreateInstance(reviewPanel).show();
    }

    document.querySelectorAll("[data-review-pager]").forEach(pager => {
        const answers = [...pager.querySelectorAll("[data-review-answer]")];
        const controls = pager.querySelector(".admin-session-review-pagination");
        if (!controls || answers.length === 0) return;

        const previous = controls.querySelector("[data-review-previous]");
        const next = controls.querySelector("[data-review-next]");
        const status = controls.querySelector("[data-review-page-status]");
        const pageCount = Math.ceil(answers.length / pageSize);
        let currentPage = 1;

        const showPage = page => {
            currentPage = Math.max(1, Math.min(page, pageCount));
            answers.forEach((answer, index) => {
                answer.hidden = index < (currentPage - 1) * pageSize ||
                    index >= currentPage * pageSize;
            });
            previous.disabled = currentPage === 1;
            next.disabled = currentPage === pageCount;
            status.textContent = `Page ${currentPage} of ${pageCount}`;
        };

        controls.hidden = pageCount <= 1;
        previous.addEventListener("click", () => showPage(currentPage - 1));
        next.addEventListener("click", () => showPage(currentPage + 1));
        showPage(1);
    });
})();
