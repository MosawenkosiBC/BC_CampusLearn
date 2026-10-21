(() => {
    const page = document.querySelector("[data-settings-page]");
    if (!page) return;

    const links = [...page.querySelectorAll(".settings-navigation a")];
    const activate = hash => {
        links.forEach(link => link.classList.toggle(
            "is-active",
            link.getAttribute("href") === hash));
    };

    links.forEach(link => link.addEventListener("click", () => {
        activate(link.getAttribute("href"));
    }));

    if (window.location.hash && links.some(link =>
        link.getAttribute("href") === window.location.hash)) {
        activate(window.location.hash);
    }

    const parseDisplayDate = value => {
        const match = value.trim().match(
            /^(\d{2})\/(\d{2})\/(\d{4})$/);
        if (!match) return null;

        const day = Number(match[1]);
        const month = Number(match[2]);
        const year = Number(match[3]);
        const date = new Date(Date.UTC(year, month - 1, day));

        if (date.getUTCFullYear() !== year ||
            date.getUTCMonth() !== month - 1 ||
            date.getUTCDate() !== day) {
            return null;
        }

        return `${String(year).padStart(4, "0")}-${String(month)
            .padStart(2, "0")}-${String(day).padStart(2, "0")}`;
    };

    const reviewDateDisplays = [...page.querySelectorAll(
        "[data-review-date-display]")];

    reviewDateDisplays.forEach(display => {
        display.addEventListener("input", () => display.setCustomValidity(""));
    });

    [...new Set(reviewDateDisplays.map(display => display.form))]
        .filter(Boolean)
        .forEach(form => form.addEventListener("submit", event => {
            let firstInvalid = null;

            reviewDateDisplays
                .filter(display => display.form === form)
                .forEach(display => {
                    const isoDate = parseDisplayDate(display.value);
                    const hidden = form.querySelector(
                        `#${display.dataset.dateTarget}`);

                    if (!isoDate || !hidden) {
                        display.setCustomValidity(
                            "Enter a valid date in dd/mm/yyyy format.");
                        firstInvalid ??= display;
                        return;
                    }

                    display.setCustomValidity("");
                    hidden.value = isoDate;
                });

            if (firstInvalid) {
                event.preventDefault();
                firstInvalid.reportValidity();
                firstInvalid.focus();
            }
        }));

    page.ownerDocument.querySelectorAll("form[data-confirm]").forEach(form => {
        form.addEventListener("submit", event => {
            if (!window.confirm(form.dataset.confirm)) event.preventDefault();
        });
    });

    const deleteModalElement = page.ownerDocument.getElementById(
        "study-area-delete-confirm-modal");
    const deleteModal = deleteModalElement && window.bootstrap
        ? bootstrap.Modal.getOrCreateInstance(deleteModalElement)
        : null;
    const deleteName = deleteModalElement?.querySelector(
        "[data-study-area-delete-name]");
    const confirmDelete = deleteModalElement?.querySelector(
        "[data-study-area-delete-confirm]");
    let pendingDeleteForm = null;

    page.ownerDocument.querySelectorAll("[data-study-area-delete]")
        .forEach(form => {
            form.querySelector("[data-study-area-delete-trigger]")
                ?.addEventListener("click", event => {
                    pendingDeleteForm = form;
                    if (deleteName) {
                        deleteName.textContent =
                            form.dataset.studyAreaName || "this study area";
                    }

                    const editModalElement = event.currentTarget.closest(".modal");
                    const editModal = editModalElement && window.bootstrap
                        ? bootstrap.Modal.getInstance(editModalElement)
                        : null;
                    if (editModal && editModalElement) {
                        editModalElement.addEventListener(
                            "hidden.bs.modal",
                            () => deleteModal?.show(),
                            { once: true });
                        editModal.hide();
                    } else {
                        deleteModal?.show();
                    }
                });
        });

    confirmDelete?.addEventListener("click", () => {
        if (!pendingDeleteForm) return;
        confirmDelete.disabled = true;
        pendingDeleteForm.requestSubmit();
    });

    deleteModalElement?.addEventListener("hidden.bs.modal", () => {
        pendingDeleteForm = null;
        if (confirmDelete) confirmDelete.disabled = false;
    });

    page.ownerDocument.querySelectorAll(
        "[data-settings-modal][data-open-on-load='true']")
        .forEach(element => bootstrap.Modal.getOrCreateInstance(element).show());
})();
