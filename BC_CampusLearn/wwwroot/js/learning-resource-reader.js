(() => {
    const contentHost = document.querySelector("[data-student-resource-content]");
    const deltaField = document.querySelector("[data-student-resource-delta]");
    if (!contentHost || !deltaField) return;

    const storedContent = deltaField.value;
    if (!window.Quill) {
        try {
            const delta = JSON.parse(storedContent);
            contentHost.textContent = Array.isArray(delta?.ops)
                ? delta.ops.map((operation) =>
                    typeof operation.insert === "string" ? operation.insert : "").join("")
                : storedContent;
        } catch {
            contentHost.textContent = storedContent;
        }
        contentHost.classList.add("is-plain-text");
        return;
    }

    const reader = new window.Quill(contentHost, {
        theme: "bubble",
        readOnly: true,
        modules: { toolbar: false }
    });

    try {
        const delta = JSON.parse(storedContent);
        if (delta && Array.isArray(delta.ops)) {
            reader.setContents(delta, "silent");
        } else {
            reader.setText(storedContent, "silent");
        }
    } catch {
        reader.setText(storedContent, "silent");
    }
    reader.enable(false);
})();

(() => {
    const button = document.querySelector("[data-resource-detail-bookmark]");
    if (!button) return;

    const storageKey = "campuslearn-learning-resource-bookmarks";
    const resourceId = button.dataset.resourceId ?? "";
    const resourceName = button.dataset.resourceName ?? "resource";
    let bookmarks;

    const loadBookmarks = () => {
        try {
            const stored = JSON.parse(
                window.localStorage.getItem(storageKey) ?? "[]");
            return new Set(Array.isArray(stored) ? stored.map(String) : []);
        } catch {
            return new Set();
        }
    };

    const updateButton = () => {
        const isBookmarked = bookmarks.has(resourceId);
        const icon = button.querySelector("i");

        button.setAttribute("aria-pressed", String(isBookmarked));
        button.setAttribute(
            "aria-label",
            `${isBookmarked ? "Remove bookmark from" : "Bookmark"} ${resourceName}`);
        if (icon) {
            icon.className = `bi ${isBookmarked ? "bi-bookmark-fill" : "bi-bookmark"}`;
        }
    };

    bookmarks = loadBookmarks();
    updateButton();

    button.addEventListener("click", () => {
        if (!resourceId) return;

        if (bookmarks.has(resourceId)) bookmarks.delete(resourceId);
        else bookmarks.add(resourceId);

        try {
            window.localStorage.setItem(storageKey, JSON.stringify([...bookmarks]));
        } catch {
            // The control remains usable for this page when storage is unavailable.
        }
        updateButton();
    });

    window.addEventListener("storage", (event) => {
        if (event.key !== storageKey) return;
        bookmarks = loadBookmarks();
        updateButton();
    });
})();
