(() => {
    const fields = document.querySelector("[data-event-fields]");
    const template = document.querySelector("[data-event-field-template]");
    const addButton = document.querySelector("[data-add-event-field]");
    const shortTextLimit = Number(fields?.dataset.shortTextLimit) || 160;

    const configureValueInput = (row) => {
        const type = row.querySelector("[data-event-field-type]");
        const current = row.querySelector("[data-event-field-value]");
        if (!type || !current) return;

        if (type.value === "ShortText" &&
            current.value.trim().length > shortTextLimit) {
            type.value = "LongText";
        }

        const inputTypes = {
            Number: "number",
            Date: "date",
            Time: "time",
            Url: "url",
            Email: "email"
        };
        const desiredTag = type.value === "LongText" ? "TEXTAREA" :
            type.value === "YesNo" ? "SELECT" : "INPUT";
        let replacement = current;
        const currentValue = current.value;

        if (current.tagName !== desiredTag) {
            replacement = document.createElement(desiredTag.toLowerCase());
            replacement.name = current.name;
            replacement.required = true;
            replacement.dataset.eventFieldValue = "";
            if (desiredTag === "SELECT") {
                replacement.innerHTML = '<option value="true">Yes</option><option value="false">No</option>';
            } else {
                replacement.maxLength = 2000;
            }
            replacement.value = currentValue;
            current.replaceWith(replacement);
        }

        if (replacement.tagName === "INPUT") {
            replacement.type = inputTypes[type.value] || "text";
            replacement.step = type.value === "Number" ? "any" : "";
        }
    };

    const wireRow = (row) => {
        row.querySelector("[data-remove-event-field]")?.addEventListener("click", () => {
            row.remove();
            reindex();
        });
        const type = row.querySelector("[data-event-field-type]");
        type?.addEventListener("change", () => configureValueInput(row));
        configureValueInput(row);
    };

    const reindex = () => {
        if (!fields) return;
        fields.querySelectorAll("[data-event-field]").forEach((row, index) => {
            row.querySelectorAll("[name]").forEach((control) => {
                control.name = control.name.replace(/CustomFields\[\d+\]/,
                    `CustomFields[${index}]`);
            });
        });
    };

    fields?.querySelectorAll("[data-event-field]").forEach(wireRow);
    fields?.addEventListener("input", (event) => {
        const valueInput = event.target.closest("[data-event-field-value]");
        if (!valueInput) return;
        const row = valueInput.closest("[data-event-field]");
        const type = row?.querySelector("[data-event-field-type]");
        if (!row || !type || type.value !== "ShortText" ||
            valueInput.value.trim().length <= shortTextLimit) return;

        const valueLength = valueInput.value.length;
        configureValueInput(row);
        const replacement = row.querySelector("[data-event-field-value]");
        replacement?.focus();
        replacement?.setSelectionRange?.(valueLength, valueLength);
    });
    addButton?.addEventListener("click", () => {
        if (!fields || !template) return;
        const index = fields.querySelectorAll("[data-event-field]").length;
        const wrapper = document.createElement("div");
        wrapper.innerHTML = template.innerHTML.replaceAll("__index__", index).trim();
        const row = wrapper.firstElementChild;
        fields.appendChild(row);
        wireRow(row);
        row.querySelector("input")?.focus();
    });

    const createEventForm = document.querySelector("[data-create-event-form]");
    const publishToggle = createEventForm?.querySelector("[data-event-publish-toggle]");
    const draftWarning = createEventForm?.querySelector("[data-event-draft-warning]");
    const updateDraftWarning = () => {
        if (!publishToggle || !draftWarning) return;
        draftWarning.hidden = publishToggle.checked;
    };
    publishToggle?.addEventListener("change", updateDraftWarning);
    updateDraftWarning();

    const deleteModalElement = document.getElementById("delete-event-modal");
    const deleteModal = deleteModalElement && window.bootstrap
        ? bootstrap.Modal.getOrCreateInstance(deleteModalElement)
        : null;
    const deleteEventName = deleteModalElement?.querySelector("[data-delete-event-name]");
    const confirmDelete = deleteModalElement?.querySelector("[data-confirm-event-delete]");
    let pendingDeleteForm = null;

    document.querySelectorAll("[data-delete-event]").forEach((form) => {
        form.querySelector("[data-delete-event-trigger]")?.addEventListener("click", () => {
            pendingDeleteForm = form;
            if (deleteEventName) {
                deleteEventName.textContent = form.dataset.eventTitle || "this event";
            }
            deleteModal?.show();
        });
    });

    confirmDelete?.addEventListener("click", () => {
        if (!pendingDeleteForm) return;
        confirmDelete.disabled = true;
        pendingDeleteForm.submit();
    });

    deleteModalElement?.addEventListener("hidden.bs.modal", () => {
        pendingDeleteForm = null;
        if (confirmDelete) confirmDelete.disabled = false;
    });
})();
