document.addEventListener("DOMContentLoaded", () => {
    const profileTabs = document.querySelector(".tutor-profile-nav");
    const activeProfileTab = profileTabs?.querySelector(
        ".tutor-profile-nav-link.is-active");
    const mobileProfileTabs = window.matchMedia("(max-width: 767.98px)");
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    if (profileTabs && activeProfileTab && mobileProfileTabs.matches) {
        const profileTabLinks = Array.from(
            profileTabs.querySelectorAll(".tutor-profile-nav-link"));
        const activeTabIndex = profileTabLinks.indexOf(activeProfileTab);
        const indicator = document.createElement("span");
        let isNavigating = false;

        indicator.className = "tutor-profile-tab-indicator";
        indicator.setAttribute("aria-hidden", "true");
        profileTabs.append(indicator);
        profileTabs.classList.add("has-animated-indicator");

        const positionIndicator = (link, animate = true) => {
            if (!link) {
                return;
            }

            indicator.classList.toggle("is-ready", animate);
            indicator.style.width = `${link.offsetWidth}px`;
            indicator.style.transform = `translateX(${link.offsetLeft}px)`;
        };

        profileTabs.scrollLeft = Math.max(
            0,
            activeProfileTab.offsetLeft -
                ((profileTabs.clientWidth - activeProfileTab.offsetWidth) / 2));
        positionIndicator(activeProfileTab, false);
        requestAnimationFrame(() => indicator.classList.add("is-ready"));

        profileTabLinks.forEach((link, targetTabIndex) => {
            link.addEventListener("click", (event) => {
                if (isNavigating || targetTabIndex === activeTabIndex ||
                    event.button !== 0 || event.metaKey || event.ctrlKey ||
                    event.shiftKey || event.altKey || !mobileProfileTabs.matches) {
                    return;
                }

                if (reducedMotion.matches) {
                    return;
                }

                event.preventDefault();
                isNavigating = true;
                activeProfileTab.classList.remove("is-active");
                activeProfileTab.removeAttribute("aria-current");
                link.classList.add("is-active", "is-switching-to");
                link.setAttribute("aria-current", "page");
                positionIndicator(link);

                window.setTimeout(() => window.location.assign(link.href), 190);
            });
        });
    }

    document.querySelectorAll(".modal[data-open-on-load='true']").forEach((element) => {
        if (window.bootstrap) {
            window.bootstrap.Modal.getOrCreateInstance(element).show();
        }
    });

    const phoneForm = document.querySelector("[data-phone-form]");
    const phoneInput = phoneForm?.querySelector("[data-phone-input]");

    if (phoneForm && phoneInput) {
        const initialPhoneNumber = phoneInput.value.trim();

        phoneInput.addEventListener("blur", (event) => {
            const nextFocusedElement = event.relatedTarget;
            const submitButton = phoneForm.querySelector("button[type='submit']");

            if (nextFocusedElement === submitButton ||
                phoneInput.value.trim() === initialPhoneNumber) {
                return;
            }

            if (!phoneInput.checkValidity()) {
                phoneInput.reportValidity();
                return;
            }

            phoneForm.requestSubmit();
        });
    }

    const modalElement = document.querySelector("#module-change-modal");
    if (!modalElement) {
        return;
    }

    const requestType = modalElement.querySelector("[data-module-request-type]");
    const changeTypeCombobox = modalElement.querySelector("[data-change-type-combobox]");
    const changeTypeToggle = modalElement.querySelector("[data-change-type-toggle]");
    const changeTypeMenu = modalElement.querySelector("[data-change-type-menu]");
    const changeTypeSelected = modalElement.querySelector("[data-change-type-selected]");
    const changeTypeOptions = Array.from(
        modalElement.querySelectorAll("[data-change-type-option]"));
    const moduleSelector = modalElement.querySelector("[data-module-selector]");
    const moduleSearch = modalElement.querySelector("[data-module-search]");
    const moduleCombobox = modalElement.querySelector("[data-module-combobox]");
    const moduleToggle = modalElement.querySelector("[data-module-toggle]");
    const moduleMenu = modalElement.querySelector("[data-module-menu]");
    const moduleOptions = modalElement.querySelector("[data-module-options]");
    const moduleSelected = modalElement.querySelector("[data-module-selected]");
    const selectedModuleList = modalElement.querySelector("[data-selected-module-list]");
    const selectedModuleChips = modalElement.querySelector("[data-selected-module-chips]");
    const sourceOptions = moduleSelector
        ? Array.from(moduleSelector.querySelectorAll("option[data-request-type]"))
        : [];

    const closeChangeTypeMenu = () => {
        if (!changeTypeMenu || !changeTypeToggle) {
            return;
        }

        changeTypeMenu.hidden = true;
        changeTypeToggle.setAttribute("aria-expanded", "false");
    };

    const updateSelectedChangeType = () => {
        if (!requestType || !changeTypeSelected) {
            return;
        }

        const selectedOption = requestType.selectedOptions[0];
        changeTypeSelected.textContent = selectedOption?.textContent.trim() ??
            "Select a change type";
        changeTypeOptions.forEach((option) => {
            option.setAttribute(
                "aria-selected",
                String(option.dataset.value === requestType.value));
        });
    };

    const closeModuleMenu = () => {
        if (!moduleMenu || !moduleToggle) {
            return;
        }

        moduleMenu.hidden = true;
        moduleToggle.setAttribute("aria-expanded", "false");
    };

    const openModuleMenu = () => {
        if (!moduleMenu || !moduleToggle) {
            return;
        }

        moduleMenu.hidden = false;
        moduleToggle.setAttribute("aria-expanded", "true");
        moduleSearch?.focus();
    };

    const updateSelectedModuleLabel = () => {
        if (!moduleSelector || !moduleSelected) {
            return;
        }

        const selectedOptions = Array.from(moduleSelector.selectedOptions)
            .filter((option) => option.dataset.requestType);

        if (selectedOptions.length === 0) {
            moduleSelected.textContent = "Select modules";
        } else if (selectedOptions.length === 1) {
            moduleSelected.textContent = selectedOptions[0].textContent.trim();
        } else {
            moduleSelected.textContent = `${selectedOptions.length} modules selected`;
        }

        if (!selectedModuleList || !selectedModuleChips) {
            return;
        }

        selectedModuleChips.replaceChildren();
        selectedModuleList.hidden = selectedOptions.length === 0;
        selectedOptions.forEach((option) => {
            const selectedModule = document.createElement("div");
            const copy = document.createElement("div");
            const code = document.createElement("strong");
            const name = document.createElement("span");
            const removeButton = document.createElement("button");

            selectedModule.className = "tutor-selected-module";
            copy.className = "tutor-selected-module-copy";
            code.textContent = option.dataset.moduleCode ?? "Module";
            name.textContent = option.dataset.moduleName ?? option.textContent.trim();
            copy.append(code, name);

            removeButton.type = "button";
            removeButton.className = "tutor-selected-module-remove";
            removeButton.setAttribute(
                "aria-label",
                `Remove ${option.textContent.trim()} from selected modules`);

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
            removeButton.append(removeIcon);
            removeButton.addEventListener("click", () => {
                option.selected = false;
                moduleSelector.dispatchEvent(new Event("change", { bubbles: true }));
                refreshModuleOptions();
            });

            selectedModule.append(copy, removeButton);
            selectedModuleChips.append(selectedModule);
        });
    };

    const refreshModuleOptions = () => {
        if (!requestType || !moduleSelector || !moduleOptions) {
            return;
        }

        const selectedType = requestType.value;
        const searchTerm = moduleSearch?.value.trim().toLocaleLowerCase() ?? "";
        sourceOptions.forEach((option) => {
            if (option.dataset.requestType !== selectedType) {
                option.selected = false;
            }
        });

        const matches = sourceOptions.filter((option) =>
            option.dataset.requestType === selectedType &&
            option.textContent.toLocaleLowerCase().includes(searchTerm));

        moduleOptions.replaceChildren();

        if (matches.length === 0) {
            const emptyMessage = document.createElement("p");
            emptyMessage.className = "tutor-module-combobox-empty";
            emptyMessage.textContent = "No matching modules found.";
            moduleOptions.append(emptyMessage);
        } else {
            matches.forEach((option) => {
                const optionButton = document.createElement("button");
                optionButton.type = "button";
                optionButton.className =
                    "tutor-module-combobox-option tutor-module-combobox-option--multi";
                optionButton.textContent = option.textContent.trim();
                optionButton.setAttribute("role", "option");
                optionButton.setAttribute(
                    "aria-selected",
                    String(option.selected));
                optionButton.addEventListener("click", () => {
                    option.selected = !option.selected;
                    moduleSelector.dispatchEvent(new Event("change", { bubbles: true }));
                    optionButton.setAttribute("aria-selected", String(option.selected));
                    updateSelectedModuleLabel();
                });
                moduleOptions.append(optionButton);
            });
        }

        updateSelectedModuleLabel();
    };

    changeTypeToggle?.addEventListener("click", () => {
        if (changeTypeMenu?.hidden) {
            closeModuleMenu();
            changeTypeMenu.hidden = false;
            changeTypeToggle.setAttribute("aria-expanded", "true");
        } else {
            closeChangeTypeMenu();
        }
    });
    changeTypeOptions.forEach((option) => {
        option.addEventListener("click", () => {
            if (!requestType) {
                return;
            }

            requestType.value = option.dataset.value ?? "";
            requestType.dispatchEvent(new Event("change", { bubbles: true }));
            updateSelectedChangeType();
            closeChangeTypeMenu();
            changeTypeToggle?.focus();
        });
    });
    requestType?.addEventListener("change", () => {
        if (moduleSearch) {
            moduleSearch.value = "";
        }
        updateSelectedChangeType();
        refreshModuleOptions();
    });
    moduleSearch?.addEventListener("input", refreshModuleOptions);
    moduleToggle?.addEventListener("click", () => {
        if (moduleMenu?.hidden) {
            closeChangeTypeMenu();
            openModuleMenu();
        } else {
            closeModuleMenu();
        }
    });
    moduleSearch?.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeModuleMenu();
            moduleToggle?.focus();
        }
    });
    document.addEventListener("click", (event) => {
        if (moduleCombobox && !moduleCombobox.contains(event.target)) {
            closeModuleMenu();
        }
        if (changeTypeCombobox && !changeTypeCombobox.contains(event.target)) {
            closeChangeTypeMenu();
        }
    });
    modalElement.addEventListener("hidden.bs.modal", () => {
        closeModuleMenu();
        closeChangeTypeMenu();
    });
    updateSelectedChangeType();
    refreshModuleOptions();

});
