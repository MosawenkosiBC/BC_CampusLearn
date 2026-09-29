(() => {
    const pageSize = 5;

    const tabs = [...document.querySelectorAll("[data-review-tab]")];
    const panels = [...document.querySelectorAll("[data-review-panel]")];

    const activateTab = (tab, moveFocus = false) => {
        tabs.forEach(candidate => {
            const isActive = candidate === tab;
            candidate.classList.toggle("is-active", isActive);
            candidate.setAttribute("aria-selected", isActive ? "true" : "false");
            candidate.tabIndex = isActive ? 0 : -1;
        });

        panels.forEach(panel => {
            panel.hidden = panel.id !== tab.dataset.reviewTab;
        });

        if (moveFocus) tab.focus();
    };

    tabs.forEach((tab, index) => {
        tab.addEventListener("click", () => activateTab(tab));
        tab.addEventListener("keydown", event => {
            if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
            event.preventDefault();

            let nextIndex = index;
            if (event.key === "ArrowLeft") nextIndex = (index - 1 + tabs.length) % tabs.length;
            if (event.key === "ArrowRight") nextIndex = (index + 1) % tabs.length;
            if (event.key === "Home") nextIndex = 0;
            if (event.key === "End") nextIndex = tabs.length - 1;
            activateTab(tabs[nextIndex], true);
        });
    });

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
