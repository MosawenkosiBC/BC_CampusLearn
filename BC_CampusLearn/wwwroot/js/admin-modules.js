(() => {
    const fieldLabel = (field) => {
        if (field.dataset.moduleLabel) {
            return field.dataset.moduleLabel;
        }
        const label = field.id
            ? document.querySelector(`label[for="${CSS.escape(field.id)}"]`)
            : null;
        return label?.textContent?.trim().toLowerCase() || "this field";
    };

    const setFieldError = (field, message) => {
        const error = field.id
            ? document.querySelector(`[data-module-error-for="${CSS.escape(field.id)}"]`)
            : null;

        const validationTarget = field.dataset.moduleValidationTarget
            ? document.querySelector(field.dataset.moduleValidationTarget)
            : field;
        field.classList.toggle("is-invalid", Boolean(message));
        validationTarget?.classList.toggle("is-invalid", Boolean(message));
        validationTarget?.toggleAttribute("aria-invalid", Boolean(message));

        if (!error) {
            return;
        }

        if (!error.id) {
            error.id = `${field.id}-error`;
        }
        error.textContent = message;
        error.hidden = !message;
        if (message) {
            validationTarget?.setAttribute("aria-describedby", error.id);
        } else if (validationTarget?.getAttribute("aria-describedby") === error.id) {
            validationTarget.removeAttribute("aria-describedby");
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
            const focusTarget = firstInvalid.dataset.moduleFocusTarget
                ? form.querySelector(firstInvalid.dataset.moduleFocusTarget)
                : firstInvalid;
            focusTarget?.focus();
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

    const editModal = document.querySelector("[data-module-edit-modal]");
    if (editModal?.dataset.open === "true" && window.bootstrap?.Modal) {
        window.bootstrap.Modal.getOrCreateInstance(editModal).show();
    }

    const assignTutorModal = document.querySelector("[data-assign-tutor-modal]");
    if (assignTutorModal?.dataset.open === "true" && window.bootstrap?.Modal) {
        window.bootstrap.Modal.getOrCreateInstance(assignTutorModal).show();
    }

    const tutorCombobox = assignTutorModal?.querySelector("[data-tutor-combobox]");
    const tutorToggle = tutorCombobox?.querySelector("[data-tutor-combobox-toggle]");
    const tutorMenu = tutorCombobox?.querySelector("[data-tutor-combobox-menu]");
    const tutorSearch = tutorCombobox?.querySelector("[data-tutor-search]");
    const tutorSelect = assignTutorModal?.querySelector("[data-tutor-native-select]");
    const tutorSelectedLabel = tutorCombobox?.querySelector("[data-tutor-selected-label]");
    const tutorOptions = Array.from(tutorCombobox?.querySelectorAll("[data-tutor-option]") || []);
    const tutorSearchEmpty = assignTutorModal?.querySelector("[data-tutor-search-empty]");
    if (tutorCombobox && tutorToggle && tutorMenu && tutorSearch && tutorSelect) {
        const openTutorMenu = () => {
            tutorMenu.hidden = false;
            tutorToggle.setAttribute("aria-expanded", "true");
            tutorSearch.focus();
        };
        const closeTutorMenu = () => {
            tutorMenu.hidden = true;
            tutorToggle.setAttribute("aria-expanded", "false");
        };
        const filterTutors = () => {
            const term = tutorSearch.value.trim().toLowerCase();
            let visibleTutors = 0;
            tutorOptions.forEach((option) => {
                const matches = !term || option.textContent.toLowerCase().includes(term);
                option.hidden = !matches;
                if (matches) visibleTutors += 1;
            });
            if (tutorSearchEmpty) tutorSearchEmpty.hidden = visibleTutors > 0;
        };
        tutorToggle.addEventListener("click", () => {
            if (tutorMenu.hidden) openTutorMenu();
            else closeTutorMenu();
        });
        tutorSearch.addEventListener("input", filterTutors);
        tutorOptions.forEach((option) => {
            option.addEventListener("click", () => {
                tutorSelect.value = option.dataset.value || "";
                tutorSelect.dispatchEvent(new Event("change", { bubbles: true }));
                tutorOptions.forEach((item) => item.setAttribute(
                    "aria-selected", String(item === option)));
                if (tutorSelectedLabel) tutorSelectedLabel.textContent = option.textContent.trim();
                closeTutorMenu();
                tutorToggle.focus();
            });
        });
        tutorCombobox.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && !tutorMenu.hidden) {
                event.preventDefault();
                closeTutorMenu();
                tutorToggle.focus();
            }
        });
        document.addEventListener("click", (event) => {
            if (!tutorCombobox.contains(event.target)) closeTutorMenu();
        });
        assignTutorModal.addEventListener("shown.bs.modal", () => tutorToggle.focus());
        assignTutorModal.addEventListener("hidden.bs.modal", () => {
            tutorSearch.value = "";
            filterTutors();
            closeTutorMenu();
        });
    }

    const removeTutorModal = document.querySelector("[data-remove-tutor-modal]");
    removeTutorModal?.addEventListener("show.bs.modal", (event) => {
        const trigger = event.relatedTarget;
        const tutorId = trigger?.dataset.removeTutorId || "";
        const tutorName = trigger?.dataset.removeTutorName || "this tutor";
        const outstandingSessionCount = Number.parseInt(
            trigger?.dataset.outstandingSessionCount || "0", 10) || 0;
        const input = removeTutorModal.querySelector("[data-remove-tutor-input]");
        const name = removeTutorModal.querySelector("[data-remove-tutor-name]");
        const clearMessage = removeTutorModal.querySelector("[data-remove-tutor-clear]");
        const warning = removeTutorModal.querySelector("[data-remove-tutor-warning]");
        const warningCount = removeTutorModal.querySelector("[data-remove-tutor-warning-count]");
        const submit = removeTutorModal.querySelector("[data-remove-tutor-submit]");
        if (input) input.value = tutorId;
        if (name) name.textContent = tutorName;
        if (clearMessage) clearMessage.hidden = outstandingSessionCount > 0;
        if (warning) warning.hidden = outstandingSessionCount === 0;
        if (warningCount) {
            warningCount.textContent = outstandingSessionCount === 1
                ? "1 outstanding session"
                : `${outstandingSessionCount} outstanding sessions`;
        }
        if (submit) submit.disabled = outstandingSessionCount > 0;
    });
})();
