(() => {
    const root = document.querySelector("[data-tutor-session]");

    if (!root) {
        return;
    }

    const tabs = [...root.querySelectorAll("[data-profile-tab]")];
    const panels = [...root.querySelectorAll("[data-profile-panel]")];
    const mobileProfileLayout = window.matchMedia("(max-width: 575.98px)");

    function setActiveProfileTab(activeTab) {
        tabs.forEach((item) => {
            const isSelected = item === activeTab;
            item.classList.toggle("is-active", isSelected);
            item.setAttribute("aria-selected", String(isSelected));
            item.setAttribute("aria-expanded", String(isSelected));
        });

        panels.forEach((panel) => {
            const isSelected = activeTab !== null &&
                panel.dataset.profilePanel === activeTab.dataset.profileTab;
            panel.classList.toggle("is-active", isSelected);
            panel.hidden = !isSelected;
        });
    }

    function syncProfileLayout(event) {
        if (event.matches) {
            setActiveProfileTab(null);
            return;
        }

        const activeTab = tabs.find((tab) =>
            tab.classList.contains("is-active")) ?? tabs[0] ?? null;
        setActiveProfileTab(activeTab);
    }

    tabs.forEach((tab) => {
        tab.addEventListener("click", () => {
            const shouldCollapse = mobileProfileLayout.matches &&
                tab.classList.contains("is-active");
            setActiveProfileTab(shouldCollapse ? null : tab);
        });
    });

    syncProfileLayout(mobileProfileLayout);
    mobileProfileLayout.addEventListener("change", syncProfileLayout);

    const monthLabel = root.querySelector("[data-calendar-month]");
    const yearLabel = root.querySelector("[data-calendar-year]");
    const previousMonth = root.querySelector("[data-calendar-previous]");
    const nextMonth = root.querySelector("[data-calendar-next]");
    const calendarGrid = root.querySelector("[data-calendar-grid]");
    const slotOptions = root.querySelector("[data-slot-options]");

    if (!monthLabel || !yearLabel || !previousMonth || !nextMonth ||
        !calendarGrid || !slotOptions) {
        return;
    }

    const availability = [
        ...root.querySelectorAll("[data-availability-data] [data-slot-id]")
    ].map((item) => ({
        id: Number(item.dataset.slotId),
        date: new Date(item.dataset.slotTime),
        isBooked: item.dataset.slotBooked === "true"
    })).sort((left, right) => left.date - right.date);

    const moduleButtons = [
        ...root.querySelectorAll("[data-module-id]")
    ];
    const desktopModuleSearch = root.querySelector(
        "[data-desktop-module-search]");
    const desktopModuleEmpty = root.querySelector(
        "[data-desktop-module-empty]");
    const moduleSelect = root.querySelector("[data-module-select]");
    const moduleSearchTrigger = root.querySelector(
        "[data-module-search-trigger]");
    const moduleSearchTriggerLabel = root.querySelector(
        "[data-module-search-trigger-label]");
    const moduleSearchPanel = root.querySelector(
        "[data-module-search-panel]");
    const moduleSearchInput = root.querySelector(
        "[data-module-search-input]");
    const moduleSearchOptions = [
        ...root.querySelectorAll("[data-search-module-id]")
    ];
    const moduleSearchEmpty = root.querySelector(
        "[data-module-search-empty]");
    const mobileModuleLayout = window.matchMedia(
        "(max-width: 575.98px)");
    const moduleSummary = root.querySelector("[data-selection-module]");
    const timeSummary = root.querySelector("[data-selection-time]");
    const saveButton = root.querySelector("[data-save-session]");
    const monthNames = [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    let selectedModule = null;
    let selectedSlot = null;
    const today = startOfDay(new Date());
    const finalBookableMonth = new Date(
        today.getFullYear(),
        today.getMonth() + 12,
        1);
    let selectedDateKey = toDateKey(today);
    let visibleMonth = today.getMonth();
    let visibleYear = today.getFullYear();

    function selectModule(module) {
        selectedModule = module;

        moduleButtons.forEach((item) =>
            item.classList.toggle(
                "is-selected",
                module !== null &&
                Number(item.dataset.moduleId) === module.id));

        if (moduleSelect) {
            moduleSelect.value =
                module === null ? "" : String(module.id);
        }

        moduleSearchOptions.forEach((item) => {
            const isSelected = module !== null &&
                Number(item.dataset.searchModuleId) === module.id;
            item.setAttribute("aria-selected", String(isSelected));
        });

        if (moduleSearchTriggerLabel) {
            moduleSearchTriggerLabel.textContent = module
                ? `${module.code}: ${module.name}`
                : "Choose a module";
        }

        updateSummary();
    }

    function filterModules() {
        const query = moduleSearchInput?.value
            .trim()
            .toLocaleLowerCase() ?? "";
        let visibleCount = 0;

        moduleSearchOptions.forEach((option) => {
            const searchableText =
                `${option.dataset.moduleCode ?? ""} ` +
                `${option.dataset.moduleName ?? ""}`;
            const isVisible = searchableText
                .toLocaleLowerCase()
                .includes(query);

            option.hidden = !isVisible;
            visibleCount += isVisible ? 1 : 0;
        });

        if (moduleSearchEmpty) {
            moduleSearchEmpty.hidden = visibleCount !== 0;
        }
    }

    function filterDesktopModules() {
        const query = desktopModuleSearch?.value
            .trim()
            .toLocaleLowerCase() ?? "";
        let visibleCount = 0;

        moduleButtons.forEach((button) => {
            const searchableText =
                `${button.dataset.moduleCode ?? ""} ` +
                `${button.dataset.moduleName ?? ""}`;
            const isVisible = searchableText
                .toLocaleLowerCase()
                .includes(query);

            button.hidden = !isVisible;
            visibleCount += isVisible ? 1 : 0;
        });

        if (desktopModuleEmpty) {
            desktopModuleEmpty.hidden = visibleCount !== 0;
        }
    }

    function closeModuleSearch(restoreFocus = false) {
        if (!moduleSearchPanel || !moduleSearchTrigger) {
            return;
        }

        moduleSearchPanel.hidden = true;
        moduleSearchTrigger.setAttribute("aria-expanded", "false");

        if (restoreFocus) {
            moduleSearchTrigger.focus();
        }
    }

    function openModuleSearch() {
        if (!mobileModuleLayout.matches ||
            !moduleSearchPanel ||
            !moduleSearchTrigger) {
            return;
        }

        moduleSearchPanel.hidden = false;
        moduleSearchTrigger.setAttribute("aria-expanded", "true");

        if (moduleSearchInput) {
            moduleSearchInput.value = "";
            filterModules();
        }
    }

    moduleButtons.forEach((button) => {
        button.addEventListener("click", () => {
            selectModule({
                id: Number(button.dataset.moduleId),
                code: button.dataset.moduleCode,
                name: button.dataset.moduleName
            });
        });
    });

    moduleSelect?.addEventListener("change", () => {
        const option = moduleSelect.selectedOptions[0];

        selectModule(moduleSelect.value
            ? {
                id: Number(moduleSelect.value),
                code: option.dataset.moduleCode,
                name: option.dataset.moduleName
            }
            : null);
    });

    moduleSearchTrigger?.addEventListener("click", () => {
        if (moduleSearchPanel?.hidden) {
            openModuleSearch();
        } else {
            closeModuleSearch();
        }
    });

    moduleSearchInput?.addEventListener("input", filterModules);
    desktopModuleSearch?.addEventListener(
        "input",
        filterDesktopModules);

    moduleSearchOptions.forEach((option) => {
        option.addEventListener("click", () => {
            selectModule({
                id: Number(option.dataset.searchModuleId),
                code: option.dataset.moduleCode,
                name: option.dataset.moduleName
            });
            closeModuleSearch(true);
        });
    });

    moduleSearchPanel?.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            event.preventDefault();
            closeModuleSearch(true);
        }
    });

    mobileModuleLayout.addEventListener("change", (event) => {
        if (!event.matches) {
            closeModuleSearch();
        }
    });

    previousMonth.addEventListener("click", () => {
        showAdjacentMonth(-1);
    });

    nextMonth.addEventListener("click", () => {
        showAdjacentMonth(1);
    });

    function showAdjacentMonth(offset) {
        const target = new Date(
            visibleYear,
            visibleMonth + offset,
            1);
        const currentMonth = new Date(
            today.getFullYear(),
            today.getMonth(),
            1);

        if (target < currentMonth || target > finalBookableMonth) {
            return;
        }

        visibleYear = target.getFullYear();
        visibleMonth = target.getMonth();
        selectedDateKey = null;
        selectedSlot = null;
        renderCalendar();
        renderSlots();
        updateSummary();
    }

    function renderCalendar() {
        calendarGrid.replaceChildren();
        monthLabel.textContent = monthNames[visibleMonth];
        yearLabel.textContent = String(visibleYear);
        previousMonth.disabled =
            visibleYear === today.getFullYear() &&
            visibleMonth === today.getMonth();
        nextMonth.disabled =
            visibleYear === finalBookableMonth.getFullYear() &&
            visibleMonth === finalBookableMonth.getMonth();

        const firstDay = new Date(visibleYear, visibleMonth, 1);
        const mondayOffset = (firstDay.getDay() + 6) % 7;
        const gridStart = new Date(
            visibleYear,
            visibleMonth,
            1 - mondayOffset);

        for (let index = 0; index < 42; index += 1) {
            const date = new Date(gridStart);
            date.setDate(gridStart.getDate() + index);

            const dateKey = toDateKey(date);
            const daySlots = availability.filter((slot) =>
                toDateKey(slot.date) === dateKey);
            const availableSlotCount = daySlots.filter(
                (slot) => !slot.isBooked).length;
            const bookedSlotCount =
                daySlots.length - availableSlotCount;
            const bookedRatio = daySlots.length === 0
                ? 0
                : bookedSlotCount / daySlots.length;
            const hasAvailableSlots = availableSlotCount > 0;
            const isDisplayedMonth =
                date.getMonth() === visibleMonth;
            const isFutureDate =
                startOfDay(date) >= today;
            const isSelectable =
                isDisplayedMonth && isFutureDate;
            const button = document.createElement("button");
            const dayNumber = document.createElement("span");

            button.type = "button";
            button.className = "calendar-day";
            dayNumber.textContent = String(date.getDate());
            button.append(dayNumber);
            const dateLabel =
                date.toLocaleDateString(undefined, {
                    weekday: "long",
                    day: "numeric",
                    month: "long",
                    year: "numeric"
                });
            const availabilityLabel = availableSlotCount === 1
                ? "1 slot available"
                : `${availableSlotCount} slots available`;

            button.setAttribute(
                "aria-label",
                `${dateLabel}, ${availabilityLabel}`);

            button.classList.toggle(
                "is-outside",
                !isDisplayedMonth);
            button.classList.toggle(
                "is-weekend",
                date.getDay() === 0 || date.getDay() === 6);
            button.classList.toggle(
                "has-slots",
                hasAvailableSlots);
            button.classList.toggle(
                "has-bookings",
                bookedSlotCount > 0);
            button.classList.toggle(
                "is-fully-booked",
                daySlots.length > 0 &&
                bookedSlotCount === daySlots.length);
            button.classList.toggle(
                "is-selected",
                dateKey === selectedDateKey);
            button.disabled = !isSelectable;
            button.style.setProperty(
                "--booking-fill",
                `${bookedRatio * 100}%`);

            if (isSelectable) {
                button.addEventListener("click", () => {
                    selectedDateKey = dateKey;
                    selectedSlot = null;
                    renderCalendar();
                    renderSlots();
                    updateSummary();
                });
            }

            calendarGrid.append(button);
        }
    }

    function renderSlots() {
        slotOptions.replaceChildren();

        const matchingSlots = availability.filter((slot) =>
            toDateKey(slot.date) === selectedDateKey &&
            !slot.isBooked);

        if (matchingSlots.length === 0) {
            const empty = document.createElement("p");
            empty.className = "slot-empty";
            const selectedDaySlots = availability.filter((slot) =>
                toDateKey(slot.date) === selectedDateKey);
            const allSlotsBooked =
                selectedDaySlots.length > 0 &&
                selectedDaySlots.every((slot) => slot.isBooked);

            empty.textContent = !selectedDateKey
                ? "Choose a date."
                : allSlotsBooked
                    ? "All time slots for this date are booked."
                    : "No time slots are available for this date.";
            slotOptions.append(empty);
            return;
        }

        matchingSlots.forEach((slot) => {
            const button = document.createElement("button");
            const end = new Date(slot.date.getTime() + 60 * 60 * 1000);

            button.type = "button";
            button.className = "slot-option";
            button.textContent =
                `${formatTime(slot.date)} – ${formatTime(end)}`;
            button.classList.toggle(
                "is-selected",
                selectedSlot?.id === slot.id);

            button.addEventListener("click", () => {
                selectedSlot = slot;
                renderSlots();
                updateSummary();
            });

            slotOptions.append(button);
        });
    }

    function updateSummary() {
        moduleSummary.textContent = selectedModule
            ? `${selectedModule.code}: ${selectedModule.name}`
            : "No module selected";

        if (selectedSlot) {
            const end =
                new Date(selectedSlot.date.getTime() + 60 * 60 * 1000);
            timeSummary.textContent =
                `${selectedSlot.date.toLocaleDateString(undefined, {
                    weekday: "short",
                    day: "numeric",
                    month: "short",
                    year: "numeric"
                })}, ${formatTime(selectedSlot.date)} – ${formatTime(end)}`;
        } else {
            timeSummary.textContent = "No date or time selected";
        }

        const isComplete = selectedModule && selectedSlot;
        saveButton.setAttribute("aria-disabled", String(!isComplete));

        if (isComplete) {
            saveButton.href =
                `/Bookings/Create?slotId=${selectedSlot.id}` +
                `&programmeModuleId=${selectedModule.id}`;
        } else {
            saveButton.removeAttribute("href");
        }
    }

    function toDateKey(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");
        return `${year}-${month}-${day}`;
    }

    function formatTime(date) {
        return date.toLocaleTimeString([], {
            hour: "2-digit",
            minute: "2-digit",
            hour12: false
        });
    }

    function startOfDay(date) {
        return new Date(
            date.getFullYear(),
            date.getMonth(),
            date.getDate());
    }

    renderCalendar();
    renderSlots();
    updateSummary();
})();
