(() => {
    const performanceModal = document.getElementById("tutor-performance-modal");
    const customize = performanceModal?.querySelector("[data-performance-customize]");
    const datesModal = document.getElementById("tutor-performance-dates-modal");
    let returningFromDates = false;
    datesModal?.addEventListener("shown.bs.modal", () => {
        datesModal.querySelector("#performance-from")?.focus();
    });
    datesModal?.addEventListener("hidden.bs.modal", () => {
        if (!performanceModal || !window.bootstrap?.Modal) return;
        returningFromDates = true;
        window.bootstrap.Modal.getOrCreateInstance(performanceModal).show();
    });
    performanceModal?.querySelectorAll("[data-performance-photo]").forEach(photo => {
        const showInitials = () => {
            photo.hidden = true;
            photo.parentElement.querySelector("[data-performance-initials]").hidden = false;
        };
        photo.addEventListener("error", showInitials, { once: true });
        if (photo.complete && photo.naturalWidth === 0) showInitials();
    });
    const search = performanceModal?.querySelector("#tutor-performance-search");
    const rows = [...(performanceModal?.querySelectorAll("[data-performance-row]") ?? [])];
    const sortButtons = [...(performanceModal?.querySelectorAll("[data-performance-sort]") ?? [])];
    let sortKey = "completed";
    let sortDirection = "descending";
    sortButtons.forEach(button => {
        button.addEventListener("click", () => {
            const key = button.dataset.performanceSort;
            sortDirection = key === sortKey
                ? (sortDirection === "ascending" ? "descending" : "ascending")
                : (key === "name" ? "ascending" : "descending");
            sortKey = key;
            const orderedRows = [...rows].sort((left, right) => {
                const comparison = key === "name"
                    ? left.dataset.name.localeCompare(right.dataset.name, undefined, { sensitivity: "base", numeric: true })
                    : Number(left.dataset[key]) - Number(right.dataset[key]);
                return (sortDirection === "ascending" ? comparison : -comparison) || rows.indexOf(left) - rows.indexOf(right);
            });
            orderedRows.forEach(row => row.parentElement.appendChild(row));
            sortButtons.forEach(sortButton => {
                sortButton.closest("th").setAttribute("aria-sort",
                    sortButton === button ? sortDirection : "none");
            });
        });
    });
    search?.addEventListener("input", () => {
        const term = search.value.trim().toLocaleLowerCase();
        let visible = 0;
        rows.forEach(row => {
            row.hidden = !row.dataset.search.toLocaleLowerCase().includes(term);
            if (!row.hidden) visible++;
        });
        performanceModal.querySelector("[data-performance-count]").textContent =
            `${visible} tutor${visible === 1 ? "" : "s"}${term ? ` of ${rows.length}` : ""}`;
        performanceModal.querySelector("[data-performance-empty]").hidden = visible > 0;
    });
    performanceModal?.addEventListener("shown.bs.modal", () => {
        (returningFromDates ? customize : search)?.focus();
        returningFromDates = false;
    });
    if (datesModal?.dataset.open === "true" && window.bootstrap?.Modal) {
        window.bootstrap.Modal.getOrCreateInstance(datesModal).show();
    } else if (performanceModal?.dataset.open === "true" && window.bootstrap?.Modal) {
        window.bootstrap.Modal.getOrCreateInstance(performanceModal).show();
    }

    const modalElement = document.getElementById("gemini-key-modal");
    if (!modalElement) return;

    const input = modalElement.querySelector('input[type="password"], input[data-api-key]');
    const toggle = modalElement.querySelector("[data-toggle-api-key]");
    toggle?.addEventListener("click", () => {
        if (!input) return;
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        toggle.textContent = show ? "Hide" : "Show";
        toggle.setAttribute("aria-label", `${show ? "Hide" : "Show"} API key`);
    });

    if (modalElement.dataset.open === "true" && window.bootstrap?.Modal) {
        window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }
})();
