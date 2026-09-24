(() => {
    const fieldLabel = (field) => {
        const label = field.id
            ? document.querySelector(`label[for="${CSS.escape(field.id)}"]`)
            : null;
        return label?.textContent?.trim().toLowerCase() || "this field";
    };

    const setFieldError = (field, message) => {
        const error = field.id
            ? document.querySelector(`[data-module-error-for="${CSS.escape(field.id)}"]`)
            : null;

        field.classList.toggle("is-invalid", Boolean(message));
        field.toggleAttribute("aria-invalid", Boolean(message));

        if (!error) {
            return;
        }

        if (!error.id) {
            error.id = `${field.id}-error`;
        }
        error.textContent = message;
        error.hidden = !message;
        if (message) {
            field.setAttribute("aria-describedby", error.id);
        } else if (field.getAttribute("aria-describedby") === error.id) {
            field.removeAttribute("aria-describedby");
        }
    };

    const validateField = (field) => {
        const value = field.value.trim();
        let message = "";

        if (field.hasAttribute("data-module-required") && !value) {
            message = field instanceof HTMLSelectElement
                ? `Select ${fieldLabel(field)}.`
                : `Enter ${fieldLabel(field)}.`;
        }

        const maximumLength = Number.parseInt(
            field.dataset.moduleMaxlength || "", 10);
        if (!message && Number.isInteger(maximumLength) && value.length > maximumLength) {
            message = `${fieldLabel(field)} must be ${maximumLength} characters or fewer.`;
        }

        setFieldError(field, message);
        return !message;
    };

    document.querySelectorAll("[data-module-validation]").forEach((form) => {
        const fields = Array.from(form.querySelectorAll(
            "[data-module-required], [data-module-maxlength]"));

        fields.forEach((field) => {
            const eventName = field instanceof HTMLSelectElement ? "change" : "input";
            field.addEventListener(eventName, () => {
                if (field.hasAttribute("aria-invalid")) {
                    validateField(field);
                }
            });
        });

        form.addEventListener("submit", (event) => {
            let firstInvalid = null;
            fields.forEach((field) => {
                if (!validateField(field) && !firstInvalid) {
                    firstInvalid = field;
                }
            });
            if (!firstInvalid) {
                return;
            }

            event.preventDefault();
            firstInvalid.focus();
        });
    });

    document.querySelectorAll("[data-module-review-validation]").forEach((form) => {
        const note = form.querySelector("textarea[name='reviewNote']");
        const error = form.querySelector("[data-module-review-error]");
        if (!note || !error) {
            return;
        }

        const showError = (message) => {
            note.classList.toggle("is-invalid", Boolean(message));
            note.toggleAttribute("aria-invalid", Boolean(message));
            error.textContent = message;
            error.hidden = !message;
        };

        note.addEventListener("input", () => {
            if (note.hasAttribute("aria-invalid")) {
                showError("");
            }
        });

        form.addEventListener("submit", (event) => {
            const value = note.value.trim();
            const isDecline = event.submitter?.name === "approve" &&
                event.submitter.value === "false";
            let message = "";
            if (isDecline && !value) {
                message = "Enter a review note before declining this request.";
            } else if (value.length > 500) {
                message = "Review note must be 500 characters or fewer.";
            }

            showError(message);
            if (message) {
                event.preventDefault();
                note.focus();
            }
        });
    });

    const createModal = document.querySelector("[data-module-create-modal]");
    if (createModal?.dataset.open === "true" && window.bootstrap?.Modal) {
        window.bootstrap.Modal.getOrCreateInstance(createModal).show();
    }
})();
