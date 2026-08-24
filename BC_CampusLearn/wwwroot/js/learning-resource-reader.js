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
