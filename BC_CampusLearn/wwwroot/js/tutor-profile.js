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
    const moduleSelector = modalElement.querySelector("[data-module-selector]");
    const moduleSearch = modalElement.querySelector("[data-module-search]");
    const moduleCombobox = modalElement.querySelector("[data-module-combobox]");
    const moduleToggle = modalElement.querySelector("[data-module-toggle]");
    const moduleMenu = modalElement.querySelector("[data-module-menu]");
    const moduleOptions = modalElement.querySelector("[data-module-options]");
    const moduleSelected = modalElement.querySelector("[data-module-selected]");
    const sourceOptions = moduleSelector
        ? Array.from(moduleSelector.querySelectorAll("option[data-request-type]"))
        : [];

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

        const selectedOption = moduleSelector.selectedOptions[0];
        moduleSelected.textContent = selectedOption?.dataset.requestType
            ? selectedOption.textContent.trim()
            : "Select a module";
    };

    const refreshModuleOptions = () => {
        if (!requestType || !moduleSelector || !moduleOptions) {
            return;
        }

        const selectedType = requestType.value;
        const searchTerm = moduleSearch?.value.trim().toLocaleLowerCase() ?? "";
        const selectedOption = moduleSelector.selectedOptions[0];
        const selectedTypeIsValid = !selectedOption?.dataset.requestType ||
            selectedOption.dataset.requestType === selectedType;

        if (!selectedTypeIsValid) {
            moduleSelector.value = "";
        }

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
                optionButton.className = "tutor-module-combobox-option";
                optionButton.textContent = option.textContent.trim();
                optionButton.setAttribute("role", "option");
                optionButton.setAttribute(
                    "aria-selected",
                    String(moduleSelector.value === option.value));
                optionButton.addEventListener("click", () => {
                    moduleSelector.value = option.value;
                    moduleSelector.dispatchEvent(new Event("change", { bubbles: true }));
                    updateSelectedModuleLabel();
                    closeModuleMenu();
                    moduleToggle?.focus();
                });
                moduleOptions.append(optionButton);
            });
        }

        updateSelectedModuleLabel();
    };

    requestType?.addEventListener("change", () => {
        if (moduleSearch) {
            moduleSearch.value = "";
        }
        refreshModuleOptions();
    });
    moduleSearch?.addEventListener("input", refreshModuleOptions);
    moduleToggle?.addEventListener("click", () => {
        if (moduleMenu?.hidden) {
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
    });
    modalElement.addEventListener("hidden.bs.modal", closeModuleMenu);
    refreshModuleOptions();

});
