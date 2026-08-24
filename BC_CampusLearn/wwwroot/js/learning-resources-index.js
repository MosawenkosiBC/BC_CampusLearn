(() => {
    const browser = document.querySelector("[data-resource-browser]");
    if (!browser) return;

    const form = browser.querySelector("[data-resource-filters-form]");
    const nameFilter = browser.querySelector("[data-resource-name-filter]");
    const moduleFilter = browser.querySelector("[data-resource-module-filter]");
    const bookmarkedFilter = browser.querySelector("[data-resource-bookmarked-filter]");
    const clearButton = browser.querySelector("[data-resource-filters-clear]");
    const emptyClearButton = browser.querySelector("[data-resource-empty-clear]");
    const cards = Array.from(browser.querySelectorAll("[data-resource-card]"));
    const filteredEmpty = browser.querySelector("[data-resource-filtered-empty]");
    const resultsCount = browser.querySelector("[data-resource-results-count]");
    const resultsLabel = browser.querySelector("[data-resource-results-label]");
    const mobileFilterTrigger = browser.querySelector(
        "[data-resource-mobile-filter-trigger]");
    const storageKey = "campuslearn-learning-resource-bookmarks";

    const loadBookmarks = () => {
        try {
            const value = JSON.parse(window.localStorage.getItem(storageKey) ?? "[]");
            return new Set(Array.isArray(value) ? value.map(String) : []);
        } catch {
            return new Set();
        }
    };

    const bookmarks = loadBookmarks();

    const saveBookmarks = () => {
        try {
            window.localStorage.setItem(storageKey, JSON.stringify([...bookmarks]));
        } catch {
            // Filtering still works if browser storage is unavailable.
        }
    };

    const updateBookmarkButton = (card) => {
        const button = card.querySelector("[data-resource-bookmark]");
        const icon = button?.querySelector("i");
        const resourceId = card.dataset.resourceId ?? "";
        const isBookmarked = bookmarks.has(resourceId);
        const resourceName = card.dataset.resourceName ?? "resource";

        button?.setAttribute("aria-pressed", String(isBookmarked));
        button?.setAttribute(
            "aria-label",
            `${isBookmarked ? "Remove bookmark from" : "Bookmark"} ${resourceName}`);
        if (icon) {
            icon.className = `bi ${isBookmarked ? "bi-bookmark-fill" : "bi-bookmark"}`;
        }
    };

    const applyFilters = () => {
        const requestedName = nameFilter?.value.trim().toLocaleLowerCase() ?? "";
        const requestedModule = moduleFilter?.value.trim().toLocaleLowerCase() ?? "";
        const bookmarkedOnly = bookmarkedFilter?.checked ?? false;
        const hasActiveFilters = Boolean(
            requestedName || requestedModule || bookmarkedOnly);
        let visibleCount = 0;

        cards.forEach((card) => {
            const resourceId = card.dataset.resourceId ?? "";
            const resourceName = (card.dataset.resourceName ?? "").toLocaleLowerCase();
            const resourceModule = (card.dataset.resourceModule ?? "").toLocaleLowerCase();
            const matches = resourceName.includes(requestedName)
                && resourceModule.includes(requestedModule)
                && (!bookmarkedOnly || bookmarks.has(resourceId));

            card.hidden = !matches;
            visibleCount += matches ? 1 : 0;
        });

        if (filteredEmpty) filteredEmpty.hidden = visibleCount !== 0;
        if (resultsCount) resultsCount.textContent = String(visibleCount);
        if (resultsLabel) {
            resultsLabel.textContent = visibleCount === 1 ? "resource" : "resources";
        }
        mobileFilterTrigger?.classList.toggle(
            "has-active-filters",
            hasActiveFilters);
    };

    cards.forEach((card) => {
        updateBookmarkButton(card);
        card.querySelector("[data-resource-bookmark]")?.addEventListener("click", () => {
            const resourceId = card.dataset.resourceId ?? "";
            if (!resourceId) return;

            if (bookmarks.has(resourceId)) bookmarks.delete(resourceId);
            else bookmarks.add(resourceId);

            saveBookmarks();
            updateBookmarkButton(card);
            if (bookmarkedFilter?.checked) applyFilters();
        });
    });

    form?.addEventListener("submit", (event) => {
        event.preventDefault();
        applyFilters();
        if (window.matchMedia("(max-width: 767.98px)").matches) {
            browser.querySelector("[data-filter-close]")?.click();
        }
    });

    const clearFilters = () => {
        if (nameFilter) nameFilter.value = "";
        if (moduleFilter) moduleFilter.value = "";
        if (bookmarkedFilter) bookmarkedFilter.checked = false;
        applyFilters();
        nameFilter?.focus();
    };

    clearButton?.addEventListener("click", clearFilters);
    emptyClearButton?.addEventListener("click", clearFilters);
})();
