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
