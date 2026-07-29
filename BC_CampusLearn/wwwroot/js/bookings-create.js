(() => {
    if (window.jQuery?.validator?.unobtrusive) {
        window.jQuery.validator.addMethod(
            "mustbetrue",
            (_value, element) => element.checked);
        window.jQuery.validator.unobtrusive.adapters.addBool(
            "mustbetrue");
    }

    const termsToggle = document.querySelector(
        "[data-booking-terms-toggle]");
    const additionalTerms = document.querySelector(
        "[data-booking-terms-more]");
    if (termsToggle && additionalTerms) {
        const mobileTermsLayout = window.matchMedia(
            "(max-width: 575.98px)");
        let termsExpanded = false;

        const updateTermsLayout = () => {
            if (!mobileTermsLayout.matches) {
                termsToggle.hidden = true;
                additionalTerms.hidden = false;
                return;
            }

            termsToggle.hidden = false;
            termsToggle.setAttribute(
                "aria-expanded",
                String(termsExpanded));
            termsToggle.textContent =
                termsExpanded ? "Read less" : "Read more";
            additionalTerms.hidden = !termsExpanded;
        };

        termsToggle.addEventListener("click", () => {
            termsExpanded = !termsExpanded;
            updateTermsLayout();
        });

        mobileTermsLayout.addEventListener(
            "change",
            updateTermsLayout);
        updateTermsLayout();
    }

    const bookingForm = document.querySelector("[data-booking-form]");

    if (bookingForm) {
        const mobileLayout = window.matchMedia(
            "(max-width: 575.98px)");
        const stages = Array.from(
            bookingForm.querySelectorAll("[data-booking-stage]"));
        const stageButtons = Array.from(
            bookingForm.querySelectorAll(
                "[data-booking-stage-target]"));
        const progress = bookingForm.querySelector(
            ".booking-mobile-progress");

        const setFallbackError = (field, messageText) => {
            const message = Array.from(
                bookingForm.querySelectorAll("[data-valmsg-for]"))
                .find((candidate) =>
                    candidate.getAttribute("data-valmsg-for") ===
                    field.name);

            field.classList.toggle(
                "input-validation-error",
                Boolean(messageText));

            if (!message) {
                return;
            }

            message.textContent = messageText;
            message.classList.toggle(
                "field-validation-error",
                Boolean(messageText));
            message.classList.toggle(
                "field-validation-valid",
                !messageText);
        };

        const validateStage = (stageNumber) => {
            const stage = bookingForm.querySelector(
                `[data-booking-stage='${stageNumber}']`);
            const fields = Array.from(
                stage?.querySelectorAll(
                    "input, select, textarea") ?? [])
                .filter((field) =>
                    !field.disabled &&
                    field.type !== "hidden" &&
                    Boolean(field.name));
            let invalidField = null;

            fields.forEach((field) => {
                if (window.jQuery?.validator) {
                    const isValid =
                        window.jQuery(field).valid();

                    if (!isValid && !invalidField) {
                        invalidField = field;
                    }
                    return;
                }

                let errorMessage = "";

                if (field.name === "Input.Location" &&
                    !field.value.trim()) {
                    errorMessage = "Enter the session location.";
                }

                if (field.name === "Input.Summary") {
                    const length = field.value.trim().length;

                    if (!length) {
                        errorMessage = "Enter a session summary.";
                    } else if (length < 75) {
                        errorMessage =
                            "Please provide a little more detail.";
                    } else if (length > 1000) {
                        errorMessage =
                            "The session summary cannot exceed 1,000 characters.";
                    }
                }

                if (field.name === "Input.AcceptedTerms" &&
                    !field.checked) {
                    errorMessage =
                        "You must agree to the terms and conditions.";
                }

                setFallbackError(field, errorMessage);
                invalidField ??= errorMessage ? field : null;
            });

            if (!invalidField) {
                return true;
            }

            invalidField.focus();
            return false;
        };

        const renderConfirmationText = (container, values) => {
            if (!container) {
                return;
            }

            container.textContent =
                values.length ? values.join(", ") : "None added";
        };

        const updateConfirmation = () => {
            const location = bookingForm.querySelector(
                "[name='Input.Location']");
            const summary = bookingForm.querySelector(
                "[name='Input.Summary']");
            const confirmationLocation = bookingForm.querySelector(
                "[data-booking-confirm-location]");
            const confirmationSummary = bookingForm.querySelector(
                "[data-booking-confirm-summary]");
            const confirmationLinks = bookingForm.querySelector(
                "[data-booking-confirm-links]");
            const confirmationDocuments = bookingForm.querySelector(
                "[data-booking-confirm-documents]");
            const linkValues = Array.from(
                bookingForm.querySelectorAll(
                    "[data-booking-link-value]"))
                .map((input) => input.value.trim())
                .filter(Boolean);
            const documentNames = Array.from(
                bookingForm.querySelectorAll(
                    "[data-booking-document-stored]"))
                .map((input) => input.files?.[0]?.name)
                .filter(Boolean);

            if (confirmationLocation) {
                confirmationLocation.textContent =
                    location?.value.trim() || "Not provided";
            }

            if (confirmationSummary) {
                confirmationSummary.textContent =
                    summary?.value.trim() || "Not provided";
            }

            renderConfirmationText(
                confirmationLinks,
                linkValues);
            renderConfirmationText(
                confirmationDocuments,
                documentNames);
        };

        const showStage = (stageNumber, scroll = true) => {
            progress?.classList.toggle(
                "is-stage-two",
                stageNumber === "2");
            progress?.classList.toggle(
                "is-stage-three",
                stageNumber === "3");

            stages.forEach((stage) => {
                stage.classList.toggle(
                    "is-active",
                    stage.dataset.bookingStage === stageNumber);
            });

            stageButtons.forEach((button) => {
                const isActive =
                    button.dataset.bookingStageTarget === stageNumber;
                const isComplete =
                    Number(button.dataset.bookingStageTarget) <
                    Number(stageNumber);
                const number = button.querySelector(
                    "[data-booking-step-number]");

                button.classList.toggle("is-active", isActive);
                button.classList.toggle(
                    "is-complete",
                    isComplete);

                if (isActive) {
                    button.setAttribute("aria-current", "step");
                } else {
                    button.removeAttribute("aria-current");
                }

                if (number) {
                    number.textContent =
                        button.dataset.bookingStageTarget;
                }
            });

            if (stageNumber === "3") {
                updateConfirmation();
            }

            if (scroll && mobileLayout.matches) {
                progress?.scrollIntoView({
                    behavior: "smooth",
                    block: "start"
                });
            }
        };

        const moveForward = (target) => {
            for (let stageNumber = 1;
                stageNumber < Number(target);
                stageNumber++) {
                if (!validateStage(String(stageNumber))) {
                    showStage(String(stageNumber));
                    return;
                }
            }

            showStage(target);
        };

        bookingForm.querySelectorAll("[data-booking-next-to]")
            .forEach((button) => {
                button.addEventListener("click", () => {
                    moveForward(button.dataset.bookingNextTo);
                });
            });

        bookingForm.querySelectorAll("[data-booking-back-to]")
            .forEach((button) => {
                button.addEventListener("click", () => {
                    showStage(button.dataset.bookingBackTo);
                });
            });

        stageButtons.forEach((button) => {
            button.addEventListener("click", () => {
                const target =
                    button.dataset.bookingStageTarget;

                const activeStage = bookingForm.querySelector(
                    "[data-booking-stage].is-active");

                if (Number(target) >
                    Number(activeStage?.dataset.bookingStage ?? "1")) {
                    moveForward(target);
                    return;
                }

                showStage(target);
            });
        });

        bookingForm.addEventListener("submit", (event) => {
            if (!mobileLayout.matches ||
                !window.jQuery?.validator) {
                return;
            }

            const isValid =
                window.jQuery(bookingForm).valid();

            if (isValid) {
                return;
            }

            event.preventDefault();

            const invalidField = bookingForm.querySelector(
                ".input-validation-error");
            const invalidStage = invalidField?.closest(
                "[data-booking-stage]");

            if (invalidStage) {
                showStage(
                    invalidStage.dataset.bookingStage,
                    false);
                invalidField.focus();
            }
        });

        const initialStage = bookingForm.querySelector(
            "[data-booking-stage].is-active");

        if (initialStage) {
            showStage(initialStage.dataset.bookingStage, false);
        }
    }

    const links = document.querySelector("[data-booking-links]");

    if (links) {
        const input = links.querySelector("[data-booking-link-input]");
        const addButton = links.querySelector("[data-booking-link-add]");
        const list = links.querySelector("[data-booking-link-list]");
        const values = Array.from(
            links.querySelectorAll("[data-booking-link-value]"));
        const limitMessage = links.querySelector(
            "[data-booking-link-limit]");

        const getAddedCount = () =>
            values.filter((value) => value.value.trim()).length;

        const setFeedback = (message, isError = false) => {
            if (!limitMessage) {
                return;
            }

            limitMessage.textContent = message;
            limitMessage.classList.toggle("is-error", isError);
        };

        const updateLinks = () => {
            const addedCount = getAddedCount();
            const remaining = values.length - addedCount;
            const atLimit = remaining === 0;

            if (addButton) {
                addButton.disabled = atLimit;
            }

            if (input) {
                input.disabled = atLimit;
            }

            setFeedback("");
        };

        const createLinkItem = (linkValue, index) => {
            if (!list) {
                return;
            }

            const item = document.createElement("li");
            const text = document.createElement("span");
            const removeButton = document.createElement("button");

            item.className = "booking-link-item";
            item.dataset.bookingLinkItem = "";
            item.dataset.linkIndex = String(index);

            text.textContent = linkValue;

            removeButton.type = "button";
            removeButton.dataset.bookingLinkRemove = "";
            removeButton.textContent = "Remove";
            removeButton.setAttribute(
                "aria-label",
                `Remove ${linkValue}`);

            item.append(text, removeButton);
            list.append(item);
        };

        const addLink = () => {
            if (!input) {
                return;
            }

            const linkValue = input.value.trim();

            if (!linkValue) {
                setFeedback("Enter a link before selecting Add.", true);
                input.focus();
                return;
            }

            let parsedLink;

            try {
                parsedLink = new URL(linkValue);
            } catch {
                setFeedback("Enter a valid web address.", true);
                input.focus();
                return;
            }

            if (!["http:", "https:"].includes(parsedLink.protocol)) {
                setFeedback(
                    "The link must start with http:// or https://.",
                    true);
                input.focus();
                return;
            }

            const normalisedLink = parsedLink.href;

            if (values.some((value) => value.value === normalisedLink)) {
                setFeedback("This link has already been added.", true);
                input.focus();
                return;
            }

            const availableIndex = values.findIndex(
                (value) => !value.value.trim());

            if (availableIndex < 0) {
                updateLinks();
                return;
            }

            values[availableIndex].value = normalisedLink;
            createLinkItem(normalisedLink, availableIndex);
            input.value = "";
            updateLinks();
            input.focus();
        };

        addButton?.addEventListener("click", addLink);

        input?.addEventListener("keydown", (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                addLink();
            }
        });

        list?.addEventListener("click", (event) => {
            const removeButton = event.target.closest(
                "[data-booking-link-remove]");

            if (!removeButton) {
                return;
            }

            const item = removeButton.closest("[data-booking-link-item]");
            const index = Number(item?.dataset.linkIndex);

            if (Number.isInteger(index) && values[index]) {
                values[index].value = "";
            }

            item?.remove();
            updateLinks();
            input?.focus();
        });

        updateLinks();
    }

    const documents = document.querySelector(
        "[data-booking-documents]");

    if (documents) {
        let input = documents.querySelector(
            "[data-booking-document-input]");
        const addButton = documents.querySelector(
            "[data-booking-document-add]");
        const inputStore = documents.querySelector(
            "[data-booking-document-inputs]");
        const list = documents.querySelector(
            "[data-booking-document-list]");
        const feedback = documents.querySelector(
            "[data-booking-document-feedback]");
        const fileName = documents.querySelector(
            "[data-booking-document-name]");
        const maximumDocuments = 2;
        const maximumFileSize = 10 * 1024 * 1024;
        const allowedExtensions = [
            ".pdf",
            ".doc",
            ".docx",
            ".png",
            ".jpg",
            ".jpeg"
        ];
        let documentId = 0;

        const getStoredInputs = () =>
            Array.from(inputStore?.querySelectorAll(
                "[data-booking-document-stored]") ?? []);

        const setDocumentFeedback = (
            message,
            isError = false) => {
            if (!feedback) {
                return;
            }

            feedback.textContent = message;
            feedback.classList.toggle("is-error", isError);
        };

        const updateDocuments = () => {
            const atLimit =
                getStoredInputs().length >= maximumDocuments;

            if (input) {
                input.disabled = atLimit;
            }

            if (addButton) {
                addButton.disabled = atLimit;
            }

            setDocumentFeedback("");
        };

        const formatFileSize = (size) => {
            if (size < 1024 * 1024) {
                return `${Math.ceil(size / 1024)} KB`;
            }

            return `${(size / (1024 * 1024)).toFixed(1)} MB`;
        };

        const createDocumentItem = (file, id) => {
            if (!list) {
                return;
            }

            const item = document.createElement("li");
            const text = document.createElement("span");
            const removeButton = document.createElement("button");

            item.className = "booking-document-item";
            item.dataset.bookingDocumentItem = "";
            item.dataset.documentId = id;

            text.textContent =
                `${file.name} (${formatFileSize(file.size)})`;

            removeButton.type = "button";
            removeButton.dataset.bookingDocumentRemove = "";
            removeButton.textContent = "Remove";
            removeButton.setAttribute(
                "aria-label",
                `Remove ${file.name}`);

            item.append(text, removeButton);
            list.append(item);
        };

        const addDocument = () => {
            const file = input?.files?.[0];

            if (!input || !file || !inputStore) {
                setDocumentFeedback(
                    "Choose a document before selecting Add.",
                    true);
                input?.focus();
                return;
            }

            const extension = file.name
                .slice(file.name.lastIndexOf("."))
                .toLowerCase();

            if (!allowedExtensions.includes(extension)) {
                setDocumentFeedback(
                    "Use a PDF, Word document, PNG, or JPG file.",
                    true);
                input.focus();
                return;
            }

            if (file.size <= 0 || file.size > maximumFileSize) {
                setDocumentFeedback(
                    "Each document must be larger than 0 bytes " +
                        "and no more than 10 MB.",
                    true);
                input.focus();
                return;
            }

            const isDuplicate = getStoredInputs().some(
                (storedInput) => {
                    const storedFile = storedInput.files?.[0];
                    return storedFile &&
                        storedFile.name === file.name &&
                        storedFile.size === file.size;
                });

            if (isDuplicate) {
                setDocumentFeedback(
                    "This document has already been added.",
                    true);
                input.focus();
                return;
            }

            const id = String(documentId++);
            const freshInput = input.cloneNode();

            freshInput.value = "";
            freshInput.id = "booking-document";
            freshInput.dataset.bookingDocumentInput = "";

            input.id = `booking-document-file-${id}`;
            input.classList.add("booking-document-input--stored");
            input.dataset.bookingDocumentStored = "";
            input.dataset.documentId = id;
            delete input.dataset.bookingDocumentInput;

            input.replaceWith(freshInput);
            inputStore.append(input);
            input = freshInput;

            createDocumentItem(file, id);
            if (fileName) {
                fileName.textContent = "Choose a document";
            }
            updateDocuments();
            input.focus();
        };

        addButton?.addEventListener("click", addDocument);

        documents.addEventListener("keydown", (event) => {
            if (event.key === "Enter" &&
                event.target === input) {
                event.preventDefault();
                addDocument();
            }
        });

        list?.addEventListener("click", (event) => {
            const removeButton = event.target.closest(
                "[data-booking-document-remove]");

            if (!removeButton) {
                return;
            }

            const item = removeButton.closest(
                "[data-booking-document-item]");
            const id = item?.dataset.documentId;
            const storedInput = getStoredInputs().find(
                (candidate) =>
                    candidate.dataset.documentId === id);

            storedInput?.remove();
            item?.remove();
            updateDocuments();
            input?.focus();
        });

        documents.addEventListener("change", (event) => {
            if (event.target === input) {
                if (fileName) {
                    fileName.textContent =
                        input.files?.[0]?.name ??
                        "Choose a document";
                }
                setDocumentFeedback("");
            }
        });

        updateDocuments();
    }

    const summary = document.querySelector("[data-booking-summary]");
    const counter = document.querySelector(
        "[data-booking-summary-counter]");

    if (!summary || !counter) {
        return;
    }

    const maximumLength = Number(summary.dataset.maxLength);

    const updateCounter = () => {
        if (summary.value.length > maximumLength) {
            summary.value =
                summary.value.slice(0, maximumLength);
        }

        const currentLength = summary.value.length;
        const remaining = Math.max(maximumLength - currentLength, 0);

        counter.textContent =
            `${remaining.toLocaleString()} characters remaining`;
    };

    summary.addEventListener("input", updateCounter);
    updateCounter();
})();
