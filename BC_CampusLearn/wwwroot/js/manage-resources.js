(() => {
    const editor = document.querySelector("[data-resource-editor]");
    const openButtons = document.querySelectorAll("[data-resource-editor-open]");
    const resourceForm = document.querySelector(".resource-form");
    const content = document.querySelector("[data-resource-content-input]");
    const quillHost = document.querySelector("[data-resource-quill-editor]");
    const contentValidation = document.querySelector("[data-resource-content-validation]");
    const counter = document.querySelector("[data-content-count]");
    const previewButton = document.querySelector("[data-resource-preview-open]");
    const previewModal = document.querySelector("[data-resource-preview-modal]");
    const previewHost = document.querySelector("[data-resource-preview-editor]");
    const previewTopic = document.querySelector("[data-resource-preview-topic]");
    const previewModule = document.querySelector("[data-resource-preview-module]");
    const fileInput = document.querySelector("#resource-documents");
    const selectedFiles = document.querySelector("[data-selected-files]");
    const modulePicker = document.querySelector("[data-resource-module-picker]");
    const moduleTrigger = modulePicker?.querySelector("[data-resource-module-trigger]");
    const modulePanel = modulePicker?.querySelector("[data-resource-module-panel]");
    const moduleLabel = modulePicker?.querySelector("[data-resource-module-label]");
    const moduleSearch = modulePicker?.querySelector("[data-resource-module-search]");
    const moduleSelect = modulePicker?.querySelector("[data-resource-module-select]");
    const moduleOptions = modulePicker?.querySelectorAll("[data-resource-module-option]") ?? [];
    const moduleEmpty = modulePicker?.querySelector("[data-resource-module-empty]");

    const updateNewResourceButtons = () => {
        const editorIsOpen = editor?.classList.contains("is-open") ?? false;
        openButtons.forEach((button) => {
            button.hidden = editorIsOpen;
        });
    };

    openButtons.forEach((button) => {
        button.addEventListener("click", () => {
            editor?.classList.add("is-open");
            updateNewResourceButtons();
            editor?.scrollIntoView({ behavior: "smooth", block: "start" });
            window.setTimeout(() => document.querySelector("#Input_Topic")?.focus(), 350);
        });
    });
    updateNewResourceButtons();

    let quill = null;
    let previewQuill = null;
    let pendingPreviewDelta = null;
    let contentSynchronized = false;

    const editorFormats = [
        "header", "bold", "italic", "underline", "list",
        "blockquote", "code-block", "link"
    ];

    const updateCount = () => {
        if (!counter) return;
        const length = quill
            ? quill.getText().trimEnd().length
            : (content?.value.length ?? 0);
        counter.textContent = `${length} ${length === 1 ? "character" : "characters"}`;
    };

    const showContentError = (message) => {
        if (!contentValidation) return;
        contentValidation.textContent = message;
        contentValidation.classList.toggle("field-validation-error", Boolean(message));
        contentValidation.classList.toggle("field-validation-valid", !message);
    };

    const parseStoredContent = (value) => {
        if (!value?.trim()) return null;
        try {
            const parsed = JSON.parse(value);
            return parsed && Array.isArray(parsed.ops) ? parsed : null;
        } catch {
            return null;
        }
    };

    if (quillHost && content && window.Quill) {
        quill = new window.Quill(quillHost, {
            theme: "snow",
            placeholder: "Write the explanation, study notes, instructions or activity here...",
            formats: editorFormats,
            modules: {
                toolbar: [
                    [{ header: [2, 3, false] }],
                    ["bold", "italic", "underline"],
                    [{ list: "ordered" }, { list: "bullet" }],
                    ["blockquote", "code-block"],
                    ["link"],
                    ["clean"]
                ]
            }
        });

        const storedDelta = parseStoredContent(content.value);
        if (storedDelta) {
            quill.setContents(storedDelta, "silent");
        } else if (content.value.trim()) {
            quill.setText(content.value, "silent");
        }

        quill.on("text-change", () => {
            contentSynchronized = false;
            showContentError("");
            updateCount();
        });
    } else if (content) {
        content.hidden = false;
        content.addEventListener("input", updateCount);
    }

    resourceForm?.addEventListener("submit", (event) => {
        if (!quill || contentSynchronized) return;

        event.preventDefault();
        if (!quill.getText().trim()) {
            showContentError("Add the learning content.");
            quill.focus();
            quillHost?.scrollIntoView({ behavior: "smooth", block: "center" });
            return;
        }

        content.value = JSON.stringify(quill.getContents());
        contentSynchronized = true;
        resourceForm.requestSubmit(event.submitter);
    });

    const loadPreview = () => {
        if (!previewHost || !pendingPreviewDelta || !window.Quill) return;
        previewQuill ??= new window.Quill(previewHost, {
            theme: "bubble",
            readOnly: true,
            formats: editorFormats,
            modules: { toolbar: false }
        });
        previewQuill.setContents(pendingPreviewDelta, "silent");
        previewQuill.enable(false);
    };

    previewButton?.addEventListener("click", () => {
        if (!quill || !previewModal || !window.bootstrap) return;
        pendingPreviewDelta = quill.getContents();
        if (previewTopic) {
            previewTopic.textContent = document.querySelector("#Input_Topic")?.value.trim()
                || "Untitled resource";
        }
        if (previewModule) {
            const selectedModule = moduleSelect?.value !== "0"
                ? moduleLabel?.textContent.trim()
                : "";
            previewModule.textContent = selectedModule || "No module selected";
        }
        window.bootstrap.Modal.getOrCreateInstance(previewModal).show();
    });

    previewModal?.addEventListener("shown.bs.modal", loadPreview);
    updateCount();

    fileInput?.addEventListener("change", () => {
        if (!selectedFiles) return;
        const names = Array.from(fileInput.files ?? []).map((file) => file.name);
        selectedFiles.textContent = names.length
            ? `${names.length} selected: ${names.join(", ")}`
            : "";
    });

    document.querySelectorAll("[data-date-filter]").forEach((input) => {
        const updateDateState = () => {
            input.classList.toggle("has-value", Boolean(input.value));
        };
        input.addEventListener("change", updateDateState);
        updateDateState();
    });

    const closeModulePanel = (restoreFocus = false) => {
        if (!modulePanel || !moduleTrigger) return;
        modulePanel.hidden = true;
        moduleTrigger.setAttribute("aria-expanded", "false");
        if (restoreFocus) moduleTrigger.focus();
    };

    const filterModuleOptions = () => {
        const query = moduleSearch?.value.trim().toLocaleLowerCase() ?? "";
        let visibleCount = 0;

        moduleOptions.forEach((option) => {
            const matches = option.textContent.toLocaleLowerCase().includes(query);
            option.hidden = !matches;
            visibleCount += matches ? 1 : 0;
        });

        if (moduleEmpty) moduleEmpty.hidden = visibleCount !== 0;
    };

    moduleTrigger?.addEventListener("click", () => {
        if (!modulePanel) return;
        const willOpen = modulePanel.hidden;
        modulePanel.hidden = !willOpen;
        moduleTrigger.setAttribute("aria-expanded", String(willOpen));

        if (willOpen) {
            if (moduleSearch) moduleSearch.value = "";
            filterModuleOptions();
            moduleSearch?.focus();
        }
    });

    moduleSearch?.addEventListener("input", filterModuleOptions);

    moduleOptions.forEach((option) => {
        option.addEventListener("click", () => {
            if (!moduleSelect || !moduleLabel) return;
            moduleSelect.value = option.dataset.value ?? "0";
            moduleLabel.textContent = option.dataset.label ?? option.textContent.trim();
            moduleOptions.forEach((item) => item.setAttribute(
                "aria-selected",
                String(item === option)));
            moduleSelect.dispatchEvent(new Event("change", { bubbles: true }));
            closeModulePanel(true);
        });
    });

    modulePanel?.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            event.preventDefault();
            closeModulePanel(true);
        }
    });

    document.addEventListener("click", (event) => {
        if (modulePicker && !modulePicker.contains(event.target)) {
            closeModulePanel();
        }
    });
})();
