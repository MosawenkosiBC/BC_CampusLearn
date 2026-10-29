(() => {
    const mobileQuery = window.matchMedia("(max-width: 767.98px)");

    document.querySelectorAll("[data-pagination]").forEach((pagination) => {
        const targetId = pagination.dataset.paginationTarget;
        const target = targetId ? document.getElementById(targetId) : null;
        if (!target) {
            return;
        }

        const rows = Array.from(target.querySelectorAll("[data-pagination-row]"));
        const previousButton = pagination.querySelector("[data-pagination-previous]");
        const nextButton = pagination.querySelector("[data-pagination-next]");
        const pageButtons = pagination.querySelector("[data-pagination-pages]");
        const pageStatus = pagination.querySelector("[data-pagination-status]");
        const mobilePageSize = Number.parseInt(
            pagination.dataset.paginationMobilePageSize, 10);
        const defaultPageSize = Number.parseInt(
            pagination.dataset.paginationDefaultPageSize, 10);

        if (!previousButton || !nextButton || !pageButtons || !pageStatus ||
            !Number.isInteger(mobilePageSize) || mobilePageSize < 1 ||
            !Number.isInteger(defaultPageSize) || defaultPageSize < 1) {
            return;
        }

        let pageSize = mobileQuery.matches ? mobilePageSize : defaultPageSize;
        let currentPage = 1;
        const pageParameter = target.dataset.paginationPageParameter;
        const initialPage = pageParameter
            ? Number(new URL(window.location.href).searchParams.get(pageParameter))
            : 1;

        const getEligibleRows = () => rows.filter((row) =>
            row.dataset.paginationExcluded !== "true");

        const getPageNumbers = (pageCount) => {
            if (pageCount <= 7) {
                return Array.from(
                    { length: pageCount }, (_, index) => index + 1);
            }

            const candidates = new Set([
                1,
                pageCount,
                currentPage - 1,
                currentPage,
                currentPage + 1
            ]);
            return Array.from(candidates)
                .filter((page) => page >= 1 && page <= pageCount)
                .sort((left, right) => left - right);
        };

        const showPage = (requestedPage, shouldScroll = false) => {
            const eligibleRows = getEligibleRows();
            const pageCount = Math.max(
                1,
                Math.ceil(eligibleRows.length / pageSize));
            currentPage = Math.min(Math.max(requestedPage, 1), pageCount);
            if (pageParameter) {
                const url = new URL(window.location.href);
                if (currentPage === 1) {
                    url.searchParams.delete(pageParameter);
                } else {
                    url.searchParams.set(pageParameter, String(currentPage));
                }
                if (shouldScroll) url.hash = "";
                window.history.replaceState(shouldScroll
                    ? { ...window.history.state, tutorDirectoryScroll: null }
                    : window.history.state, "", url);
            }
            const firstRowIndex = (currentPage - 1) * pageSize;
            const lastRowIndex = Math.min(
                firstRowIndex + pageSize, eligibleRows.length);
            const rowsOnPage = new Set(
                eligibleRows.slice(firstRowIndex, lastRowIndex));

            rows.forEach((row) => {
                row.hidden = !rowsOnPage.has(row);
            });

            pagination.hidden = pageCount <= 1;
            previousButton.disabled = currentPage === 1;
            nextButton.disabled = currentPage === pageCount;
            pageButtons.replaceChildren();

            let previousPageNumber = 0;
            getPageNumbers(pageCount).forEach((pageNumber) => {
                if (pageNumber - previousPageNumber > 1) {
                    const ellipsis = document.createElement("span");
                    ellipsis.className = "campus-pagination__ellipsis";
                    ellipsis.setAttribute("aria-hidden", "true");
                    ellipsis.textContent = "…";
                    pageButtons.append(ellipsis);
                }

                const button = document.createElement("button");
                button.type = "button";
                button.className = "campus-pagination__page";
                button.textContent = String(pageNumber);
                button.setAttribute("aria-label", `Page ${pageNumber}`);
                if (pageNumber === currentPage) {
                    button.classList.add("is-current");
                    button.setAttribute("aria-current", "page");
                }
                button.addEventListener(
                    "click", () => showPage(pageNumber, true));
                pageButtons.append(button);
                previousPageNumber = pageNumber;
            });

            pageStatus.textContent = eligibleRows.length === 0
                ? "No items to display."
                : `Page ${currentPage} of ${pageCount}. Showing items ${firstRowIndex + 1} through ${lastRowIndex} of ${eligibleRows.length}.`;

            if (shouldScroll) {
                target.scrollIntoView({
                    behavior: window.matchMedia(
                        "(prefers-reduced-motion: reduce)").matches
                        ? "auto"
                        : "smooth",
                    block: "start"
                });
            }
        };

        const updatePageSize = () => {
            const nextPageSize = mobileQuery.matches
                ? mobilePageSize
                : defaultPageSize;
            if (nextPageSize === pageSize) {
                return;
            }

            const firstVisibleRowIndex = (currentPage - 1) * pageSize;
            pageSize = nextPageSize;
            currentPage = Math.floor(firstVisibleRowIndex / pageSize) + 1;
            showPage(currentPage);
        };

        previousButton.addEventListener(
            "click", () => showPage(currentPage - 1, true));
        nextButton.addEventListener(
            "click", () => showPage(currentPage + 1, true));
        target.addEventListener("campus:pagination-refresh", (event) => {
            const resetPage = event instanceof CustomEvent &&
                event.detail?.resetPage === true;
            showPage(resetPage ? 1 : currentPage);
        });
        mobileQuery.addEventListener("change", updatePageSize);

        showPage(Number.isSafeInteger(initialPage) && initialPage > 0 ? initialPage : 1);
    });
})();
