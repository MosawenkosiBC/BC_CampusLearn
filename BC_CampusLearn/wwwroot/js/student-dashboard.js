(() => {
    document.querySelectorAll("[data-module-subscription-control]")
        .forEach((control) => {
            const picker = control.querySelector("[data-module-subscription-picker]");
            const pickerTrigger = picker?.querySelector("[data-module-subscription-trigger]");
            const pickerPanel = picker?.querySelector("[data-module-subscription-panel]");
            const pickerValue = picker?.querySelector("[data-module-subscription-value]");
            const pickerSearch = picker?.querySelector("[data-module-subscription-search]");
            const pickerOptions = picker?.querySelectorAll("[data-module-subscription-option]") ?? [];
            const pickerNoResults = picker?.querySelector("[data-module-subscription-no-results]");
            const selectedCode = control.querySelector("[data-module-subscription-code]");
            const form = control.querySelector("[data-module-subscription-form]");
            const validationMessage = control.querySelector("[data-module-subscription-validation]");

            const closePicker = (restoreFocus = false) => {
                if (!pickerPanel || !pickerTrigger) return;
                pickerPanel.hidden = true;
                pickerTrigger.setAttribute("aria-expanded", "false");
                if (restoreFocus) pickerTrigger.focus();
            };

            const filterPickerOptions = () => {
                const query = pickerSearch?.value.trim().toLocaleLowerCase() ?? "";
                let visibleCount = 0;
                pickerOptions.forEach((option) => {
                    const searchableText = `${option.dataset.moduleCode ?? ""} ${option.dataset.moduleName ?? ""}`
                        .toLocaleLowerCase();
                    const matches = searchableText.includes(query);
                    option.hidden = !matches;
                    visibleCount += matches ? 1 : 0;
                });
                if (pickerNoResults) pickerNoResults.hidden = visibleCount !== 0;
            };

            pickerTrigger?.addEventListener("click", () => {
                if (!pickerPanel) return;
                const willOpen = pickerPanel.hidden;
                pickerPanel.hidden = !willOpen;
                pickerTrigger.setAttribute("aria-expanded", String(willOpen));
                if (willOpen) {
                    if (pickerSearch) pickerSearch.value = "";
                    filterPickerOptions();
                    pickerSearch?.focus();
                }
            });

            pickerSearch?.addEventListener("input", filterPickerOptions);

            pickerOptions.forEach((option) => {
                option.addEventListener("click", () => {
                    const code = option.dataset.moduleCode ?? "";
                    const name = option.dataset.moduleName ?? "";
                    if (selectedCode) selectedCode.value = code;
                    if (pickerValue) pickerValue.textContent = `${code}: ${name}`;
                    pickerOptions.forEach((item) => item.setAttribute(
                        "aria-selected", String(item === option)));
                    pickerTrigger?.classList.remove("is-invalid");
                    pickerTrigger?.setAttribute("aria-invalid", "false");
                    if (validationMessage) validationMessage.hidden = true;
                    closePicker(true);
                });
            });

            form?.addEventListener("submit", (event) => {
                if (selectedCode?.value.trim()) return;

                event.preventDefault();
                pickerTrigger?.classList.add("is-invalid");
                pickerTrigger?.setAttribute("aria-invalid", "true");
                if (validationMessage) {
                    validationMessage.textContent = form.dataset.validationMessage
                        ?? "Select a module before continuing.";
                    validationMessage.hidden = false;
                }
                pickerTrigger?.focus();
            });

            pickerPanel?.addEventListener("keydown", (event) => {
                if (event.key === "Escape") {
                    event.preventDefault();
                    closePicker(true);
                }
            });

            document.addEventListener("click", (event) => {
                if (picker && !picker.contains(event.target)) closePicker();
            });
        });

    const countdown = document.querySelector(
        "[data-student-session-countdown]");

    if (!countdown) {
        return;
    }

    const sessionStart = new Date(countdown.dataset.sessionStart);

    if (Number.isNaN(sessionStart.getTime())) {
        return;
    }

    const pluralize = (value, singular, plural) =>
        `${value} ${value === 1 ? singular : plural}`;

    const formatCountdown = (milliseconds) => {
        if (milliseconds <= 0) {
            return "Starting now";
        }

        const totalMinutes = Math.max(
            1,
            Math.ceil(milliseconds / 60000));

        if (totalMinutes < 60) {
            return `${pluralize(totalMinutes, "min", "mins")} left`;
        }

        const totalHours = Math.floor(totalMinutes / 60);
        const minutes = totalMinutes % 60;

        if (totalHours < 24) {
            const hoursLabel = pluralize(totalHours, "hr", "hrs");
            return minutes === 0
                ? `${hoursLabel} left`
                : `${hoursLabel} ${pluralize(minutes, "min", "mins")} left`;
        }

        const days = Math.floor(totalHours / 24);
        const hours = totalHours % 24;
        const daysLabel = pluralize(days, "day", "days");

        return hours === 0
            ? `${daysLabel} left`
            : `${daysLabel} ${pluralize(hours, "hr", "hrs")} left`;
    };

    const updateCountdown = () => {
        countdown.textContent = formatCountdown(
            sessionStart.getTime() - Date.now());
    };

    updateCountdown();
    window.setInterval(updateCountdown, 30000);
})();

(() => {
    const cards = document.querySelectorAll("[data-dashboard-resource-card]");
    if (cards.length === 0) return;

    const storageKey = "campuslearn-learning-resource-bookmarks";
    let bookmarks;
    try {
        const stored = JSON.parse(window.localStorage.getItem(storageKey) ?? "[]");
        bookmarks = new Set(Array.isArray(stored) ? stored.map(String) : []);
    } catch {
        bookmarks = new Set();
    }

    const saveBookmarks = () => {
        try {
            window.localStorage.setItem(storageKey, JSON.stringify([...bookmarks]));
        } catch {
            // Bookmark controls remain usable when storage is unavailable.
        }
    };

    const updateBookmark = (card, button) => {
        const resourceId = card.dataset.resourceId ?? "";
        const resourceName = card.dataset.resourceName ?? "resource";
        const isBookmarked = bookmarks.has(resourceId);
        const icon = button.querySelector("i");

        button.setAttribute("aria-pressed", String(isBookmarked));
        button.setAttribute(
            "aria-label",
            `${isBookmarked ? "Remove bookmark from" : "Bookmark"} ${resourceName}`);
        if (icon) {
            icon.className = `bi ${isBookmarked ? "bi-bookmark-fill" : "bi-bookmark"}`;
        }
    };

    cards.forEach((card) => {
        const button = card.querySelector("[data-dashboard-resource-bookmark]");
        if (!button) return;

        updateBookmark(card, button);
        button.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();

            const resourceId = card.dataset.resourceId ?? "";
            if (!resourceId) return;
            if (bookmarks.has(resourceId)) bookmarks.delete(resourceId);
            else bookmarks.add(resourceId);

            saveBookmarks();
            updateBookmark(card, button);
        });
    });
})();
