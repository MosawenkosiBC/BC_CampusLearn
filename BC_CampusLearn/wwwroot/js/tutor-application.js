(() => {
    const introduction = document.querySelector(
        "[data-application-intro]");
    const agreeButton = document.querySelector(
        "[data-application-agree]");
    const loader = document.querySelector(
        "[data-application-loader]");
    const application = document.querySelector(
        "[data-application-flow]");
    const existingApplication = document.querySelector(
        "[data-existing-application]");
    const existingApplicationCloseButtons = document.querySelectorAll(
        "[data-existing-application-close]");
    const applicationSuccess = document.querySelector(
        "[data-application-success]");
    const applicationSuccessCloseButtons = document.querySelectorAll(
        "[data-application-success-close]");
    const siteFooter = document.querySelector("body > footer");
    const orientationAccordionTriggers = Array.from(
        document.querySelectorAll(
            "[data-orientation-accordion-trigger]"));
    const mobileAccordion = window.matchMedia("(max-width: 767.98px)");

    if (!introduction ||
        !agreeButton ||
        !loader ||
        !application) {
        return;
    }

    const stepPanels = Array.from(
        application.querySelectorAll("[data-application-step]"));
    const stageItems = Array.from(
        application.querySelectorAll("[data-application-stage]"));
    const submissionForm = application.querySelector(
        "#tutor-application-submit-form");
    const submitButton = submissionForm?.querySelector(
        "[data-application-submit]");
    const submitLabel = submissionForm?.querySelector(
        "[data-application-submit-label]");
    const phoneInput = application.querySelector(
        "[name='Input.PhoneNumber']");

    const syncOrientationAccordion = () => {
        orientationAccordionTriggers.forEach(trigger => {
            trigger.setAttribute(
                "aria-expanded",
                mobileAccordion.matches ? "false" : "true");
        });
    };

    orientationAccordionTriggers.forEach(trigger => {
        trigger.addEventListener("click", () => {
            if (!mobileAccordion.matches) {
                return;
            }

            const shouldOpen =
                trigger.getAttribute("aria-expanded") !== "true";

            orientationAccordionTriggers.forEach(item => {
                item.setAttribute("aria-expanded", "false");
            });

            trigger.setAttribute("aria-expanded", String(shouldOpen));
        });
    });

    syncOrientationAccordion();
    mobileAccordion.addEventListener("change", syncOrientationAccordion);

    applicationSuccess?.showModal();

    phoneInput?.addEventListener("input", () => {
        phoneInput.value = phoneInput.value
            .replace(/\D/g, "")
            .slice(0, 10);
    });

    const findValidationMessage = (control) =>
        Array.from(application.querySelectorAll("[data-valmsg-for]"))
            .find(message => message.dataset.valmsgFor === control.name);

    const setControlError = (control, message) => {
        const validationMessage = findValidationMessage(control);
        const hasError = Boolean(message);

        control.classList.toggle("is-invalid", hasError);
        control.setAttribute("aria-invalid", String(hasError));

        if (validationMessage) {
            validationMessage.textContent = message;
            validationMessage.classList.toggle(
                "field-validation-error",
                hasError);
            validationMessage.classList.toggle(
                "field-validation-valid",
                !hasError);
        }
    };

    const validateControl = (control) => {
        const value = control.value?.trim() ?? "";
        const isFile = control.type === "file";
        const isEmpty = isFile
            ? !control.files?.length
            : !value;
        let message = "";

        if (control.required && isEmpty) {
            message = control.dataset.valRequired ??
                "Complete this required field.";
        } else if (control.name === "Input.PhoneNumber" &&
            !/^\d{10}$/.test(value)) {
            message = "Enter a 10-digit phone number.";
        } else if (control.name === "Input.OverallAverage" &&
            (Number(value) < 65 || Number(value) > 100)) {
            message = "Academic average must be between 65 and 100.";
        } else if (control.type === "url" && value) {
            try {
                const url = new URL(value);

                if (url.protocol !== "http:" && url.protocol !== "https:") {
                    message = "Enter a valid demonstration URL.";
                }
            } catch {
                message = "Enter a valid demonstration URL.";
            }
        }

        setControlError(control, message);
        return !message;
    };

    const validateStep = (panel) => {
        const controls = Array.from(panel.querySelectorAll(
            "input[name]:not([type='hidden']), " +
            "select[name], textarea[name]"));
        let firstInvalidControl = null;

        controls.forEach(control => {
            if (!validateControl(control) && !firstInvalidControl) {
                firstInvalidControl = control;
            }
        });

        firstInvalidControl?.focus();
        return firstInvalidControl === null;
    };

    application.querySelectorAll(
        "input[name]:not([type='hidden']), select[name], textarea[name]")
        .forEach(control => {
            const eventName = control.tagName === "SELECT" ||
                control.type === "file"
                ? "change"
                : "input";

            control.addEventListener(eventName, () => {
                if (control.classList.contains("is-invalid")) {
                    validateControl(control);
                }
            });
        });

    application.querySelectorAll("[data-tutor-document-input]")
        .forEach(input => {
            const fileName = input.parentElement?.querySelector(
                "[data-tutor-document-name]");
            const defaultLabel = fileName?.textContent.trim() ?? "Choose file";

            input.addEventListener("change", () => {
                if (fileName) {
                    fileName.textContent = input.files?.[0]?.name ??
                        defaultLabel;
                }
            });
        });

    application.querySelectorAll("[data-character-input]")
        .forEach(input => {
            const counter = input.parentElement?.querySelector(
                "[data-character-counter]");
            const maximumLength = Number(input.dataset.maxLength);

            if (!counter || !maximumLength) {
                return;
            }

            const updateCounter = () => {
                if (input.value.length > maximumLength) {
                    input.value = input.value.slice(0, maximumLength);
                }

                const remaining = Math.max(
                    maximumLength - input.value.length,
                    0);

                counter.textContent =
                    `${remaining.toLocaleString()} characters remaining`;
            };

            input.addEventListener("input", updateCounter);
            updateCounter();
        });

    const moduleSelector = application.querySelector(
        "[data-module-selector]");
    const moduleList = moduleSelector?.querySelector("[data-module-list]");
    const moduleStatus = moduleSelector?.querySelector(
        "[data-module-status]");
    const moduleTrigger = moduleSelector?.querySelector(
        "[data-module-trigger]");
    const moduleTriggerLabel = moduleSelector?.querySelector(
        "[data-module-trigger-label]");
    const modulePanel = moduleSelector?.querySelector("[data-module-panel]");
    const moduleSearch = moduleSelector?.querySelector("[data-module-search]");
    const selectedModuleList = moduleSelector?.querySelector(
        "[data-selected-module-list]");
    const selectedModuleInputs = moduleSelector?.querySelector(
        "[data-selected-module-inputs]");
    const moduleFeedback = application.querySelector(
        "[data-module-feedback]");
    const moduleCounter = application.querySelector(
        "[data-module-counter]");
    const yearSelect = application.querySelector(
        "[name='Input.YearOfStudy']");
    let loadedModuleContext = "";
    let availableModules = [];
    const selectedModuleIds = new Set(
        Array.from(
            selectedModuleInputs?.querySelectorAll(
                "input[name='FinalInput.ProgrammeModuleIds']") ?? [])
            .map(input => input.value));

    const updateModuleSelection = () => {
        if (!moduleSelector ||
            !moduleCounter ||
            !moduleFeedback ||
            !moduleTriggerLabel ||
            !selectedModuleList ||
            !selectedModuleInputs) {
            return;
        }

        const selectedCount = selectedModuleIds.size;
        const minimum = Number(moduleSelector.dataset.minimum);
        const maximum = Number(moduleSelector.dataset.maximum);

        moduleCounter.textContent = `${selectedCount} of ${maximum} selected`;
        moduleTriggerLabel.textContent = selectedCount
            ? `${selectedCount} module${selectedCount === 1 ? "" : "s"} selected`
            : "Choose modules";
        moduleFeedback.textContent = selectedCount < minimum
            ? `Select at least ${minimum} modules.`
            : "";

        moduleList?.querySelectorAll("[data-module-option]")
            .forEach(option => {
                const isSelected = selectedModuleIds.has(
                    option.dataset.moduleId);
                option.setAttribute("aria-selected", String(isSelected));
                option.disabled = !isSelected && selectedCount >= maximum;
            });

        selectedModuleList.replaceChildren();
        selectedModuleInputs.replaceChildren();

        availableModules
            .filter(module => selectedModuleIds.has(
                String(module.programmeModuleId)))
            .forEach(module => {
                const chip = document.createElement("div");
                chip.className = "tutor-selected-module";

                const copy = document.createElement("div");
                copy.className = "tutor-selected-module-copy";

                const code = document.createElement("strong");
                code.textContent = module.moduleCode;

                const name = document.createElement("span");
                name.textContent = module.moduleName;

                copy.append(code, name);

                const remove = document.createElement("button");
                remove.type = "button";
                remove.setAttribute(
                    "aria-label",
                    `Remove ${module.moduleCode}: ${module.moduleName}`);

                const removeIcon = document.createElementNS(
                    "http://www.w3.org/2000/svg",
                    "svg");
                removeIcon.setAttribute("viewBox", "0 0 24 24");
                removeIcon.setAttribute("aria-hidden", "true");

                const removePath = document.createElementNS(
                    "http://www.w3.org/2000/svg",
                    "path");
                removePath.setAttribute("d", "M6 6l12 12M18 6L6 18");
                removePath.setAttribute("fill", "none");
                removePath.setAttribute("stroke", "currentColor");
                removePath.setAttribute("stroke-linecap", "round");
                removePath.setAttribute("stroke-width", "2");
                removeIcon.append(removePath);
                remove.append(removeIcon);
                remove.addEventListener("click", () => {
                    selectedModuleIds.delete(String(module.programmeModuleId));
                    updateModuleSelection();
                });

                const input = document.createElement("input");
                input.type = "hidden";
                input.name = "FinalInput.ProgrammeModuleIds";
                input.value = String(module.programmeModuleId);

                chip.append(copy, remove);
                selectedModuleList.append(chip);
                selectedModuleInputs.append(input);
            });
    };

    const filterModuleOptions = () => {
        if (!moduleList || !moduleStatus) {
            return;
        }

        const query = moduleSearch?.value
            .trim()
            .toLocaleLowerCase() ?? "";
        let visibleCount = 0;

        moduleList.querySelectorAll("[data-module-option]")
            .forEach(option => {
                const searchableText =
                    `${option.dataset.moduleCode ?? ""} ` +
                    `${option.dataset.moduleName ?? ""} ` +
                    `${option.dataset.programmeName ?? ""}`;
                const isVisible = searchableText
                    .toLocaleLowerCase()
                    .includes(query);

                option.hidden = !isVisible;
                visibleCount += isVisible ? 1 : 0;
            });

        moduleStatus.hidden = visibleCount !== 0;

        if (!visibleCount) {
            moduleStatus.textContent = "No matching modules found.";
        }
    };

    const createModuleOption = (module) => {
        const option = document.createElement("button");
        option.type = "button";
        option.className = "tutor-module-option";
        option.dataset.moduleOption = "";
        option.dataset.moduleId = String(module.programmeModuleId);
        option.dataset.moduleCode = module.moduleCode;
        option.dataset.moduleName = module.moduleName;
        option.dataset.programmeName = module.programmeName;
        option.setAttribute("role", "option");
        option.setAttribute("aria-selected", "false");

        const code = document.createElement("strong");
        code.textContent = module.moduleCode;

        const details = document.createElement("span");
        details.textContent =
            `${module.moduleName} · Year ${module.yearOfStudy} · ` +
            module.programmeName;

        option.append(code, details);
        option.addEventListener("click", () => {
            const moduleId = String(module.programmeModuleId);
            const maximum = Number(moduleSelector.dataset.maximum);

            if (selectedModuleIds.has(moduleId)) {
                selectedModuleIds.delete(moduleId);
            } else if (selectedModuleIds.size < maximum) {
                selectedModuleIds.add(moduleId);
            }

            updateModuleSelection();
        });

        return option;
    };

    const renderEligibleModules = (modules) => {
        if (!moduleList || !moduleStatus) {
            return;
        }

        availableModules = modules;
        moduleList.replaceChildren();
        const availableIds = new Set(
            modules.map(module => String(module.programmeModuleId)));

        selectedModuleIds.forEach(moduleId => {
            if (!availableIds.has(moduleId)) {
                selectedModuleIds.delete(moduleId);
            }
        });

        if (!modules.length) {
            moduleStatus.hidden = false;
            moduleStatus.textContent =
                "No eligible modules were found for this year of study.";
            updateModuleSelection();
            return;
        }

        moduleStatus.hidden = true;
        modules.forEach(module => {
            moduleList.append(createModuleOption(module));
        });

        updateModuleSelection();
        filterModuleOptions();
    };

    const loadEligibleModules = async () => {
        if (!moduleSelector ||
            !moduleList ||
            !moduleStatus ||
            !yearSelect) {
            return;
        }

        const yearOfStudy = yearSelect.value;
        const context = yearOfStudy;

        if (!yearOfStudy || context === loadedModuleContext) {
            return;
        }

        moduleStatus.hidden = false;
        moduleStatus.textContent = "Loading eligible modules…";
        moduleList.replaceChildren();

        try {
            const endpoint = new URL(
                moduleSelector.dataset.modulesEndpoint,
                window.location.origin);
            endpoint.searchParams.set("yearOfStudy", yearOfStudy);

            const response = await fetch(endpoint);

            if (!response.ok) {
                throw new Error("Unable to load modules.");
            }

            const modules = await response.json();
            loadedModuleContext = context;
            renderEligibleModules(modules);
        } catch {
            moduleStatus.hidden = false;
            moduleStatus.textContent =
                "We could not load the eligible modules. Please try again.";
        }
    };

    const closeModulePanel = (restoreFocus = false) => {
        if (!modulePanel || !moduleTrigger) {
            return;
        }

        modulePanel.hidden = true;
        moduleTrigger.setAttribute("aria-expanded", "false");

        if (restoreFocus) {
            moduleTrigger.focus();
        }
    };

    moduleTrigger?.addEventListener("click", () => {
        if (!modulePanel) {
            return;
        }

        const shouldOpen = modulePanel.hidden;
        modulePanel.hidden = !shouldOpen;
        moduleTrigger.setAttribute("aria-expanded", String(shouldOpen));

        if (shouldOpen) {
            moduleSearch?.focus();
        }
    });

    moduleSearch?.addEventListener("input", filterModuleOptions);
    moduleSearch?.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            closeModulePanel(true);
        }
    });

    document.addEventListener("click", event => {
        if (moduleSelector && !moduleSelector.contains(event.target)) {
            closeModulePanel();
        }
    });

    const showStep = (stepNumber) => {
        const targetPanel = stepPanels.find(
            panel => Number(panel.dataset.applicationStep) === stepNumber);

        if (!targetPanel) {
            return;
        }

        if (stepNumber === 3) {
            void loadEligibleModules();
        }

        stepPanels.forEach(panel => {
            panel.hidden = panel !== targetPanel;
        });

        stageItems.forEach(stageItem => {
            const stageNumber = Number(stageItem.dataset.applicationStage);
            const isActive = stageNumber === stepNumber;

            stageItem.classList.toggle("is-active", isActive);
            stageItem.classList.toggle(
                "is-complete",
                stageNumber < stepNumber);

            if (isActive) {
                stageItem.setAttribute("aria-current", "step");
            } else {
                stageItem.removeAttribute("aria-current");
            }
        });

        const stepHeading = targetPanel.querySelector("h2");
        stepHeading?.focus({ preventScroll: true });
        application.scrollIntoView({
            behavior: "smooth",
            block: "start"
        });
    };

    const initialStep = Number(application.dataset.initialStep);

    if (initialStep > 0) {
        introduction.hidden = true;
        loader.hidden = true;
        application.hidden = false;
        siteFooter?.setAttribute("hidden", "");
        showStep(initialStep);
    }

    submissionForm?.addEventListener("submit", event => {
        const minimumModules = Number(
            moduleSelector?.dataset.minimum ?? 2);

        if (!validateStep(
            submissionForm.closest("[data-application-step]"))) {
            event.preventDefault();
            return;
        }

        if (selectedModuleIds.size < minimumModules) {
            event.preventDefault();
            moduleFeedback.textContent =
                `Select at least ${minimumModules} modules.`;
            moduleTrigger?.focus();
            return;
        }

        submissionForm.querySelectorAll("[data-application-mirror]")
            .forEach(input => input.remove());

        application.querySelectorAll(
            "[data-application-step='1'] [name], " +
            "[data-application-step='2'] [name]")
            .forEach(control => {
                if (control.disabled ||
                    ((control.type === "checkbox" ||
                      control.type === "radio") &&
                     !control.checked)) {
                    return;
                }

                const mirror = document.createElement("input");
                mirror.type = "hidden";
                mirror.name = control.name;
                mirror.value = control.value;
                mirror.dataset.applicationMirror = "true";
                submissionForm.append(mirror);
            });

        if (submitButton) {
            submitButton.disabled = true;
        }

        if (submitLabel) {
            submitLabel.textContent = "Submitting...";
        }
    });

    application
        .querySelectorAll("[data-application-next], [data-application-back]")
        .forEach(button => {
            const targetValue = button.dataset.applicationNext ??
                button.dataset.applicationBack;
            const targetStep = Number(targetValue);

            if (!stepPanels.some(
                panel => Number(panel.dataset.applicationStep) === targetStep)) {
                return;
            }

            button.addEventListener("click", () => {
                if (button.hasAttribute("data-application-next")) {
                    const currentStep = button.closest(
                        "[data-application-step]");

                    if (currentStep && !validateStep(currentStep)) {
                        return;
                    }
                }

                showStep(targetStep);
            });
        });

    agreeButton.addEventListener("click", () => {
        if (existingApplication) {
            existingApplication.showModal();
            return;
        }

        agreeButton.disabled = true;
        siteFooter?.setAttribute("hidden", "");
        introduction.inert = true;
        loader.hidden = false;

        window.setTimeout(() => {
            introduction.hidden = true;
            loader.hidden = true;
            application.hidden = false;
            application.focus({ preventScroll: true });
            application.scrollIntoView({
                behavior: "smooth",
                block: "start"
            });
        }, 2100);
    });

    existingApplicationCloseButtons.forEach(button => {
        button.addEventListener("click", () => {
            existingApplication?.close();
        });
    });

    existingApplication?.addEventListener("click", event => {
        if (event.target === existingApplication) {
            existingApplication.close();
        }
    });

    applicationSuccessCloseButtons.forEach(button => {
        button.addEventListener("click", () => {
            applicationSuccess?.close();
        });
    });

    applicationSuccess?.addEventListener("click", event => {
        if (event.target === applicationSuccess) {
            applicationSuccess.close();
        }
    });
})();
