(() => {
    const input = document.querySelector("[data-session-search]");
    const count = document.querySelector("[data-session-result-count]");
    const results = document.getElementById("admin-session-results");
    const empty = results?.querySelector("[data-session-empty]");
    if (!input || !count || !results || !empty) return;

    const rows = [...results.querySelectorAll("[data-pagination-row]")];
    const normalize = value => (value ?? "").trim().toLocaleLowerCase();

    input.addEventListener("input", () => {
        const search = input.value.trim();
        const query = normalize(search);
        let matches = 0;

        for (const row of rows) {
            const { tutorName, tutorNumber, studentName, sessionLocation } = row.dataset;
            const visible = [tutorName, tutorNumber, studentName, sessionLocation]
                .some(value => normalize(value).includes(query));
            row.dataset.paginationExcluded = String(!visible);
            row.hidden = !visible;
            if (visible) matches++;
        }

        count.textContent = String(matches);
        empty.hidden = matches !== 0;
        results.dispatchEvent(new CustomEvent("campus:pagination-refresh", {
            detail: { resetPage: true }
        }));

        const url = new URL(window.location.href);
        if (search) {
            url.searchParams.set("Search", search);
        } else {
            url.searchParams.delete("Search");
        }
        window.history.replaceState(window.history.state, "", url);
    });
})();
