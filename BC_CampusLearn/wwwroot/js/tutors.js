(() => {
    const panel = document.querySelector("[data-filter-panel]");
    const openButton = document.querySelector("[data-filter-open]");
    const closeButtons = document.querySelectorAll("[data-filter-close]");

    if (!panel || !openButton || closeButtons.length === 0) {
        return;
    }

    const filterBreakpoint =
        panel.dataset.filterBreakpoint || "767.98";
    const mobileQuery = window.matchMedia(
        `(max-width: ${filterBreakpoint}px)`);
    let lastFocusedElement = null;

    const setAccessibilityState = () => {
        if (mobileQuery.matches && !panel.classList.contains("is-open")) {
            panel.setAttribute("aria-hidden", "true");
            panel.removeAttribute("role");
            panel.removeAttribute("aria-modal");
            panel.inert = true;
        } else {
            panel.removeAttribute("aria-hidden");
            panel.inert = false;
        }
    };

    const openFilters = () => {
        if (!mobileQuery.matches) {
            return;
        }

        lastFocusedElement = document.activeElement;
        panel.classList.add("is-open");
        document.body.classList.add("tutor-filter-open");
        openButton.setAttribute("aria-expanded", "true");
        panel.setAttribute("role", "dialog");
        panel.setAttribute("aria-modal", "true");
        panel.removeAttribute("aria-hidden");
        panel.inert = false;
        panel.querySelector("[data-filter-close]")?.focus();
    };

    const closeFilters = () => {
        panel.classList.remove("is-open");
        document.body.classList.remove("tutor-filter-open");
        openButton.setAttribute("aria-expanded", "false");
        setAccessibilityState();

        if (lastFocusedElement instanceof HTMLElement) {
            lastFocusedElement.focus();
        }
    };

    openButton.addEventListener("click", openFilters);
    closeButtons.forEach(button =>
        button.addEventListener("click", closeFilters));

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && panel.classList.contains("is-open")) {
            closeFilters();
        }
    });

    mobileQuery.addEventListener("change", () => {
        if (!mobileQuery.matches) {
            closeFilters();
        } else {
            setAccessibilityState();
        }
    });

    setAccessibilityState();
})();

(() => {
    const form = document.querySelector("[data-live-tutor-search]");
    const searchInputs = form?.querySelectorAll(
        "[data-live-tutor-search-input]");
    const dateInput = form?.querySelector(
        "[data-live-tutor-date-input]");
    const resultsContent = document.querySelector(
        "[data-tutor-results-content]");
    const resultsCount = document.querySelector(
        "[data-tutor-results-count]");
    const emptyResults = document.querySelector(
        "[data-tutor-empty-results]");
    const filterButton = document.querySelector("[data-filter-open]");
    const tutorCards = [
        ...document.querySelectorAll("[data-tutor-search-card]")
    ];

    if (!form || !searchInputs?.length || !dateInput || !resultsContent ||
        !resultsCount || !emptyResults) {
        return;
    }

    const normalize = (value) => value
        .trim()
        .toLocaleLowerCase();

    const updateUrl = () => {
        const parameters = new URLSearchParams(new FormData(form));

        [...parameters.entries()].forEach(([key, value]) => {
            if (!value.trim() || (key === "ShowAllTutors" && value === "false")) {
                parameters.delete(key);
            }
        });

        const query = parameters.toString();
        window.history.replaceState(
            {},
            "",
            `${window.location.pathname}${query ? `?${query}` : ""}`);
    };

    const updateFilterIndicator = () => {
        if (!filterButton) {
            return;
        }

        const values = new FormData(form);
        const hasActiveFilters = [...values.entries()].some(([key, value]) =>
            key === "ShowAllTutors"
                ? value === "true"
                : value.trim().length > 0);
        filterButton.classList.toggle(
            "has-active-filters",
            hasActiveFilters);
    };

    const filterTutors = () => {
        const nameQuery = normalize(searchInputs[0].value);
        const moduleQuery = normalize(searchInputs[1].value);
        const dateQuery = dateInput.value;
        let visibleCount = 0;

        tutorCards.forEach((card) => {
            const matchesName = normalize(card.dataset.tutorName ?? "")
                .includes(nameQuery);
            const matchesModule = normalize(card.dataset.tutorModules ?? "")
                .includes(moduleQuery);
            const availableDates = (
                card.dataset.tutorAvailableDates ?? "").split(" ");
            const matchesDate = !dateQuery ||
                availableDates.includes(dateQuery);
            const isVisible = matchesName && matchesModule && matchesDate;

            card.hidden = !isVisible;
            visibleCount += isVisible ? 1 : 0;
        });

        resultsCount.textContent = String(visibleCount);
        emptyResults.hidden = visibleCount !== 0;
        updateFilterIndicator();
        updateUrl();
    };

    searchInputs.forEach((input) =>
        input.addEventListener("input", filterTutors));
    dateInput.addEventListener("change", filterTutors);
})();
