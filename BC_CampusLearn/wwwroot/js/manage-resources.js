(() => {
    const editor = document.querySelector("[data-resource-editor]");
    const creationWorkspace = document.querySelector("[data-resource-creation-workspace]");
    const resizeHandle = document.querySelector("[data-resource-resize-handle]");
    const openButtons = document.querySelectorAll("[data-resource-editor-open]");
    const resourceForm = document.querySelector(".resource-form");
    const publishButton = resourceForm?.querySelector('[name="submitAction"][value="publish"]');
    const commentsInput = resourceForm?.querySelector('#Input_AllowSubscriberComments');
    const publishDialog = document.querySelector('[data-resource-publish-dialog]');
    let publishConfirmed = false;
    const content = document.querySelector("[data-resource-content-input]");
    const quillHost = document.querySelector("[data-resource-quill-editor]");
    const contentValidation = document.querySelector("[data-resource-content-validation]");
    const counter = document.querySelector("[data-content-count]");
    const previewHost = document.querySelector("[data-resource-preview-editor]");
    const previewTopic = document.querySelector("[data-resource-preview-topic]");
    const previewModule = document.querySelector("[data-resource-preview-module]");
    const previewReading = document.querySelector("[data-resource-preview-reading]");
    const previewLinks = document.querySelector("[data-resource-preview-links]");
    const previewDocuments = document.querySelector("[data-resource-preview-documents]");
    const previewBody = document.querySelector("[data-resource-preview-body]");
    const previewModuleRow = document.querySelector("[data-resource-preview-module-row]");
    const mobilePreviewModal = document.querySelector("[data-resource-mobile-preview-modal]");
    const mobilePreviewContent = document.querySelector("[data-resource-mobile-preview-content]");
    const mobilePreviewOpen = document.querySelector("[data-resource-preview-open]");
    const topicInput = document.querySelector("#Input_Topic");
    const linkInputs = [
        document.querySelector("#Input_Link1"),
        document.querySelector("#Input_Link2")
    ].filter(Boolean);
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
        creationWorkspace?.classList.toggle("is-open", editorIsOpen);
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
            content.value = JSON.stringify(quill.getContents());
            showContentError("");
            updateCount();
            updatePreview();
        });
    } else if (content) {
        content.hidden = false;
        content.addEventListener("input", () => {
            updateCount();
            updatePreview();
        });
    }

    // Synchronize before other form validators inspect the hidden content field.
    resourceForm?.addEventListener("submit", (event) => {
        if (!quill) return;

        if (!quill.getText().trim()) {
            event.preventDefault();
            showContentError("Add the learning content.");
            quill.focus();
            quillHost?.scrollIntoView({ behavior: "smooth", block: "center" });
            return;
        }

        content.value = JSON.stringify(quill.getContents());
    }, true);

    resourceForm?.addEventListener("submit", (event) => {
        if (event.defaultPrevented || event.submitter !== publishButton || publishConfirmed) return;
        event.preventDefault();
        publishDialog?.showModal();
    });

    document.querySelectorAll('[data-resource-publish-comments]').forEach((button) => {
        button.addEventListener("click", () => {
            if (!resourceForm || !publishButton || !commentsInput) return;
            commentsInput.value = button.dataset.resourcePublishComments;
            publishDialog.close();
            publishConfirmed = true;
            try {
                // This runs from the choice click, after the original submit event ended.
                resourceForm.requestSubmit(publishButton);
            } finally {
                publishConfirmed = false;
            }
        });
    });
    document.querySelector('[data-resource-publish-back]')?.addEventListener("click", () => publishDialog?.close());

    const previewDocumentMarkup = previewDocuments?.innerHTML ?? "";

    const getSafeUrl = (value) => {
        try {
            const url = new URL(value);
            return ["http:", "https:"].includes(url.protocol) ? url.href : null;
        } catch {
            return null;
        }
    };

    const updatePreviewLinks = () => {
        if (!previewLinks) return false;
        previewLinks.replaceChildren();

        linkInputs.forEach((input) => {
            const rawUrl = input.value.trim();
            const url = getSafeUrl(rawUrl);
            if (!url) return;

            const link = document.createElement("a");
            link.href = url;
            link.target = "_blank";
            link.rel = "noopener";
            link.innerHTML = '<i class="bi bi-box-arrow-up-right" aria-hidden="true"></i>';
            link.append(` ${rawUrl}`);
            previewLinks.append(link);
        });

        return previewLinks.childElementCount > 0;
    };

    const updatePreviewDocuments = () => {
        if (!previewDocuments) return false;
        previewDocuments.innerHTML = previewDocumentMarkup;

        Array.from(fileInput?.files ?? []).forEach((file) => {
            const documentItem = document.createElement("span");
            documentItem.className = "resource-live-preview-document";
            documentItem.innerHTML = '<i class="bi bi-file-earmark-arrow-down" aria-hidden="true"></i>';
            documentItem.append(` ${file.name}`);
            previewDocuments.append(documentItem);
        });

        return previewDocuments.childElementCount > 0;
    };

    const updatePreview = () => {
        const hasTopic = Boolean(topicInput?.value.trim());
        const hasModule = moduleSelect?.value !== "0";
        if (previewTopic) {
            previewTopic.textContent = topicInput?.value.trim() || "";
            previewTopic.hidden = !hasTopic;
        }
        if (previewModule) {
            const selectedModule = hasModule
                ? moduleLabel?.textContent.trim()
                : "";
            previewModule.textContent = selectedModule || "";
        }
        if (previewModuleRow) previewModuleRow.hidden = !hasModule;

        let hasContent = false;
        if (previewHost && quill && window.Quill) {
            previewQuill ??= new window.Quill(previewHost, {
                theme: "bubble",
                readOnly: true,
                formats: editorFormats,
                modules: { toolbar: false }
            });
            hasContent = Boolean(quill.getText().trim());
            previewQuill.setContents(quill.getContents(), "silent");
            previewQuill.enable(false);
            previewHost.hidden = !hasContent;
        } else if (previewHost && content) {
            hasContent = Boolean(content.value.trim());
            previewHost.textContent = content.value;
            previewHost.hidden = !hasContent;
        }

        const hasLinks = updatePreviewLinks();
        const hasDocuments = updatePreviewDocuments();
        if (previewReading) previewReading.hidden = !hasLinks && !hasDocuments;
        if (previewBody) {
            previewBody.hidden = !hasTopic && !hasModule && !hasContent && !hasLinks && !hasDocuments;
        }
    };

    const renderMobilePreview = () => {
        if (!mobilePreviewContent || !previewBody) return;
        const preview = previewBody.cloneNode(true);
        preview.hidden = false;
        preview.removeAttribute("data-resource-preview-body");
        mobilePreviewContent.replaceChildren(preview);
    };

    mobilePreviewOpen?.addEventListener("click", () => {
        if (!mobilePreviewModal || !window.bootstrap) return;
        updatePreview();
        renderMobilePreview();
        if (mobilePreviewModal.parentElement !== document.body) {
            document.body.append(mobilePreviewModal);
        }
        window.bootstrap.Modal.getOrCreateInstance(mobilePreviewModal).show();
    });

    topicInput?.addEventListener("input", updatePreview);
    linkInputs.forEach((input) => input.addEventListener("input", updatePreview));

    updateCount();
    updatePreview();

    fileInput?.addEventListener("change", () => {
        if (!selectedFiles) return;
        const names = Array.from(fileInput.files ?? []).map((file) => file.name);
        selectedFiles.textContent = names.length
            ? `${names.length} selected: ${names.join(", ")}`
            : "";
        updatePreview();
    });

    const setEditorWidth = (width) => {
        if (!creationWorkspace) return;
        const workspaceWidth = creationWorkspace.getBoundingClientRect().width;
        const handleWidth = resizeHandle?.getBoundingClientRect().width ?? 18;
        const minimumWidth = 320;
        const maximumWidth = Math.max(minimumWidth, workspaceWidth - handleWidth - minimumWidth);
        const clampedWidth = Math.min(Math.max(width, minimumWidth), maximumWidth);
        creationWorkspace.style.gridTemplateColumns = `${clampedWidth}px ${handleWidth}px minmax(${minimumWidth}px, 1fr)`;
        resizeHandle?.setAttribute("aria-valuenow", String(Math.round(clampedWidth)));
    };

    let resizeStartX = 0;
    let resizeStartWidth = 0;
    const onResizeMove = (event) => setEditorWidth(resizeStartWidth + event.clientX - resizeStartX);
    const stopResize = () => {
        document.body.classList.remove("resource-resizing");
        window.removeEventListener("pointermove", onResizeMove);
        window.removeEventListener("pointerup", stopResize);
    };

    resizeHandle?.addEventListener("pointerdown", (event) => {
        if (!creationWorkspace) return;
        resizeStartX = event.clientX;
        resizeStartWidth = editor?.getBoundingClientRect().width ?? 0;
        document.body.classList.add("resource-resizing");
        window.addEventListener("pointermove", onResizeMove);
        window.addEventListener("pointerup", stopResize, { once: true });
    });

    resizeHandle?.addEventListener("keydown", (event) => {
        if (!["ArrowLeft", "ArrowRight"].includes(event.key)) return;
        event.preventDefault();
        const currentWidth = editor?.getBoundingClientRect().width ?? 0;
        setEditorWidth(currentWidth + (event.key === "ArrowLeft" ? -40 : 40));
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
            updatePreview();
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
