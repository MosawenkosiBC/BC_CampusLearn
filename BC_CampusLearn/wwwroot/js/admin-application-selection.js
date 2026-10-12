(() => {
    const checkboxes = Array.from(document.querySelectorAll("[data-applicant-select]"));
    const selectVisible = document.querySelector("[data-select-visible-applicants]");
    const openButton = document.querySelector("[data-reject-selected-open]");
    const modal = document.querySelector("[data-reject-selected-modal]");
    const form = document.querySelector("[data-reject-selected-form]");
    const inputs = form?.querySelector("[data-selected-applicant-inputs]");
    if (!selectVisible || !openButton || !modal || !form || !inputs) {
        return;
    }

    const selected = () => checkboxes.filter(checkbox => checkbox.checked);
    const visible = () => checkboxes.filter(checkbox => !checkbox.closest("[data-pagination-row]").hidden);
    const update = () => {
        const count = selected().length;
        document.querySelector("[data-applicant-selection-count]").textContent = `${count} selected`;
        openButton.disabled = count === 0;
        const visibleRows = visible();
        const checkedCount = visibleRows.filter(checkbox => checkbox.checked).length;
        selectVisible.disabled = visibleRows.length === 0;
        selectVisible.checked = visibleRows.length > 0 && checkedCount === visibleRows.length;
        selectVisible.indeterminate = checkedCount > 0 && checkedCount < visibleRows.length;
    };

    checkboxes.forEach(checkbox => checkbox.addEventListener("change", update));
    selectVisible.addEventListener("change", () => {
        visible().forEach(checkbox => { checkbox.checked = selectVisible.checked; });
        update();
    });
    openButton.addEventListener("click", () => {
        if (selected().length === 0) return;
        modal.querySelector("[data-reject-selected-count]").textContent = String(selected().length);
        modal.showModal();
    });
    modal.addEventListener("click", event => {
        if (event.target === modal) modal.close();
    });
    form.addEventListener("submit", event => {
        const applicants = selected();
        if (applicants.length === 0) {
            event.preventDefault();
            modal.close();
            return;
        }
        inputs.replaceChildren();
        applicants.forEach(checkbox => {
            const input = document.createElement("input");
            input.type = "hidden";
            input.name = "selectedTutorIds";
            input.value = checkbox.value;
            inputs.append(input);
        });
        const button = form.querySelector("button[type='submit']");
        button.disabled = true;
        button.textContent = "Rejecting and deleting…";
    });

    const list = document.getElementById("application-candidates");
    if (list) {
        new MutationObserver(update).observe(list, {
            subtree: true, attributes: true, attributeFilter: ["hidden"]
        });
    }
    update();
})();
