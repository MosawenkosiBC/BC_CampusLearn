(() => {
    const initializeSearch = (inputSelector, rowSelector, emptySelector) => {
        const searchInput = document.querySelector(inputSelector);
        if (!searchInput) return;

        const rows = [...document.querySelectorAll(rowSelector)];
        const emptyState = document.querySelector(emptySelector);

        searchInput.addEventListener("input", () => {
            const query = searchInput.value.trim().toLocaleLowerCase();
            let visibleRows = 0;

            rows.forEach((row) => {
                const matches = (row.dataset.searchText ?? "").includes(query);
                row.hidden = !matches;
                if (matches) visibleRows += 1;
            });

            if (emptyState) emptyState.hidden = visibleRows !== 0;
        });
    };

    initializeSearch(
        "[data-module-assignment-search]",
        "[data-module-assignment-row]",
        "[data-module-assignment-empty]");
    initializeSearch(
        "[data-booked-module-search]",
        "[data-booked-module-row]",
        "[data-booked-module-empty]");

    const initializePeriodFields = (selectSelector, datesSelector) => {
        const periodSelect = document.querySelector(selectSelector);
        const customDates = document.querySelector(datesSelector);
        if (!periodSelect || !customDates) return;

        const updateCustomDates = () => {
            customDates.hidden = periodSelect.value !== "custom";
        };
        periodSelect.addEventListener("change", updateCustomDates);
        updateCustomDates();
    };

    initializePeriodFields(
        "[data-booking-period-select]",
        "[data-booking-custom-dates]");
    initializePeriodFields(
        "[data-review-period-select]",
        "[data-review-custom-dates]");

    const bookedModulesModal = document.querySelector("#all-booked-modules-modal");
    if (bookedModulesModal?.dataset.autoOpen === "true" && window.bootstrap) {
        window.bootstrap.Modal.getOrCreateInstance(bookedModulesModal).show();
    }
})();
