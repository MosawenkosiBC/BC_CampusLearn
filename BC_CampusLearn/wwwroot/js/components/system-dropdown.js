(() => {
    const closeDropdown = (dropdown, returnFocus = false) => {
        const trigger = dropdown.querySelector("[data-system-dropdown-trigger]");
        const panel = dropdown.querySelector("[data-system-dropdown-panel]");
        if (!trigger || !panel || panel.hidden) {
            return;
        }

        panel.hidden = true;
        trigger.setAttribute("aria-expanded", "false");
        dropdown.classList.remove("is-open");
        if (returnFocus) {
            trigger.focus();
        }
    };

    const closeOtherDropdowns = (current) => {
        document.querySelectorAll("[data-system-dropdown-root]")
            .forEach((dropdown) => {
                if (dropdown !== current) {
                    closeDropdown(dropdown);
                }
            });
    };

    document.querySelectorAll("select[data-system-dropdown]")
        .forEach((select, index) => {
            const dropdown = document.createElement("div");
            dropdown.className = "system-dropdown";
            dropdown.dataset.systemDropdownRoot = "";
            select.before(dropdown);
            dropdown.append(select);
            select.classList.add("system-dropdown__native");
            select.tabIndex = -1;
            select.setAttribute("aria-hidden", "true");

            const trigger = document.createElement("button");
            trigger.type = "button";
            trigger.className = "system-dropdown__trigger";
            trigger.dataset.systemDropdownTrigger = "";
            trigger.disabled = select.disabled;
            trigger.id = `${select.id || `system-dropdown-${index}`}-trigger`;
            trigger.setAttribute("aria-haspopup", "listbox");
            trigger.setAttribute("aria-expanded", "false");

            const value = document.createElement("span");
            value.className = "system-dropdown__value";
            value.dataset.systemDropdownValue = "";
            value.id = `${trigger.id}-value`;
            value.textContent = select.selectedOptions[0]?.textContent ?? "Select";
            trigger.append(value);

            const chevron = document.createElement("span");
            chevron.className = "system-dropdown__chevron";
            chevron.setAttribute("aria-hidden", "true");
            trigger.append(chevron);

            const panel = document.createElement("div");
            panel.id = `${select.id || `system-dropdown-${index}`}-options`;
            panel.className = "system-dropdown__panel";
            panel.dataset.systemDropdownPanel = "";
            panel.setAttribute("role", "listbox");
            panel.hidden = true;
            trigger.setAttribute("aria-controls", panel.id);

            const label = select.id
                ? document.querySelector(`label[for="${select.id}"]`)
                : null;
            if (label) {
                label.id ||= `${select.id}-label`;
                label.htmlFor = trigger.id;
                trigger.setAttribute(
                    "aria-labelledby", `${label.id} ${value.id}`);
                panel.setAttribute("aria-labelledby", label.id);
            } else {
                trigger.setAttribute("aria-label", "Select an option");
            }

            const optionButtons = Array.from(select.options).map((option) => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "system-dropdown__option";
                button.dataset.systemDropdownOption = option.value;
                button.textContent = option.textContent;
                button.setAttribute("role", "option");
                button.setAttribute(
                    "aria-selected", String(option.selected));
                button.disabled = option.disabled;
                panel.append(button);
                return button;
            });

            const selectOption = (button) => {
                select.value = button.dataset.systemDropdownOption ?? "";
                value.textContent = button.textContent;
                optionButtons.forEach((optionButton) => {
                    optionButton.setAttribute(
                        "aria-selected", String(optionButton === button));
                });
                select.dispatchEvent(new Event("change", { bubbles: true }));
                closeDropdown(dropdown, true);
            };

            optionButtons.forEach((button) => {
                button.addEventListener("click", () => selectOption(button));
                button.addEventListener("keydown", (event) => {
                    const enabledOptions = optionButtons.filter(
                        (option) => !option.disabled);
                    const currentIndex = enabledOptions.indexOf(button);
                    let nextIndex = currentIndex;

                    if (event.key === "ArrowDown") {
                        nextIndex = (currentIndex + 1) % enabledOptions.length;
                    } else if (event.key === "ArrowUp") {
                        nextIndex = (currentIndex - 1 + enabledOptions.length) %
                            enabledOptions.length;
                    } else if (event.key === "Home") {
                        nextIndex = 0;
                    } else if (event.key === "End") {
                        nextIndex = enabledOptions.length - 1;
                    } else if (event.key === "Escape") {
                        event.preventDefault();
                        closeDropdown(dropdown, true);
                        return;
                    } else {
                        return;
                    }

                    event.preventDefault();
                    enabledOptions[nextIndex]?.focus();
                });
            });

            trigger.addEventListener("click", () => {
                const willOpen = panel.hidden;
                closeOtherDropdowns(dropdown);
                panel.hidden = !willOpen;
                trigger.setAttribute("aria-expanded", String(willOpen));
                dropdown.classList.toggle("is-open", willOpen);
                if (willOpen) {
                    const selected = optionButtons.find(
                        (button) => button.getAttribute("aria-selected") === "true");
                    (selected ?? optionButtons.find(
                        (button) => !button.disabled))?.focus();
                }
            });

            trigger.addEventListener("keydown", (event) => {
                if (!["ArrowDown", "ArrowUp", "Enter", " "].includes(event.key)) {
                    return;
                }

                event.preventDefault();
                if (panel.hidden) {
                    trigger.click();
                }
            });

            dropdown.addEventListener("focusout", () => {
                window.setTimeout(() => {
                    if (!dropdown.contains(document.activeElement)) {
                        closeDropdown(dropdown);
                    }
                }, 0);
            });

            dropdown.append(trigger, panel);
        });

    document.addEventListener("click", (event) => {
        document.querySelectorAll("[data-system-dropdown-root]")
            .forEach((dropdown) => {
                if (!dropdown.contains(event.target)) {
                    closeDropdown(dropdown);
                }
            });
    });
})();
