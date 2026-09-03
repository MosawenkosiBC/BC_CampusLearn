(() => {
    const calendar = document.querySelector(
        "[data-availability-calendar]");

    if (!calendar) {
        return;
    }

    const grid = calendar.querySelector("[data-calendar-grid]");
    const monthHeading = calendar.querySelector(
        "[data-calendar-month]");
    const previousButton = calendar.querySelector(
        "[data-calendar-previous]");
    const nextButton = calendar.querySelector("[data-calendar-next]");
    const valueInput = calendar.querySelector("[data-calendar-value]");
    const currentMonthOnly =
        calendar.dataset.currentMonthOnly === "true";
    const deleteTimeSlotIcon =
        '<svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 24 24" aria-hidden="true"><path d="M0 0h24v24H0z" fill="none"/><path fill="none" stroke="#f00" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M1 5h22m-8.75-4h-4.5a1.5 1.5 0 0 0-1.5 1.5V5h7.5V2.5a1.5 1.5 0 0 0-1.5-1.5m-4.5 16.75v-7.5m4.5 7.5v-7.5m4.61 11.37A1.49 1.49 0 0 1 17.37 23H6.63a1.49 1.49 0 0 1-1.49-1.38L3.75 5h16.5z"/></svg>';

    const parseDate = (value) => {
        const [year, month, day] = value.split("-").map(Number);
        return new Date(year, month - 1, day);
    };

    const occupiedScheduleElement = document.querySelector(
        "[data-occupied-schedule-times]");
    const occupiedScheduleValues = occupiedScheduleElement?.dataset
        .occupiedScheduleTimes
        .split("|")
        .filter(Boolean) ?? [];
    const toScheduleMinute = (dateValue, timeValue) => {
        const [year, month, day] = dateValue.split("-").map(Number);
        const [hours, minutes] = timeValue.split(":").map(Number);
        return Date.UTC(year, month - 1, day, hours, minutes) / 60000;
    };
    const occupiedScheduleMinutes = occupiedScheduleValues.map((value) => {
        const [dateValue, timeValue] = value.split("T");
        return toScheduleMinute(dateValue, timeValue);
    });
    const overlapsSchedule = (candidateMinutes, otherMinutes) =>
        otherMinutes.some((minutes) =>
            Math.abs(candidateMinutes - minutes) < 75);
    const overlapWarningElement = document.querySelector(
        "[data-availability-overlap-warning-modal]");
    const overlapWarningMessage = document.querySelector(
        "[data-availability-overlap-warning-message]");
    let modalToRestore = null;

    const showOverlapWarning = (time, sourceModal = null) => {
        const message = `${time} overlaps another availability, ` +
            "time slots must be 75 minutes apart to avoid double booking";

        if (overlapWarningMessage) {
            overlapWarningMessage.textContent = message;
        }

        if (!overlapWarningElement || !window.bootstrap?.Modal) {
            return message;
        }

        const warningModal = window.bootstrap.Modal.getOrCreateInstance(
            overlapWarningElement);

        if (sourceModal?.classList.contains("show")) {
            modalToRestore = sourceModal;
            sourceModal.addEventListener("hidden.bs.modal", () => {
                warningModal.show();
            }, { once: true });
            window.bootstrap.Modal.getOrCreateInstance(sourceModal).hide();
        } else {
            warningModal.show();
        }

        return "";
    };

    overlapWarningElement?.addEventListener("hidden.bs.modal", () => {
        if (!modalToRestore) {
            return;
        }

        window.bootstrap.Modal.getOrCreateInstance(modalToRestore).show();
        modalToRestore = null;
    });

    const formatValue = (date) => {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");
        return `${year}-${month}-${day}`;
    };

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const minimumDate = calendar.dataset.minDate
        ? parseDate(calendar.dataset.minDate)
        : today;
    let selectedDate = valueInput.value
        ? parseDate(valueInput.value)
        : new Date(today);
    let displayedMonth = new Date(
        selectedDate.getFullYear(),
        selectedDate.getMonth(),
        1);

    const monthFormatter = new Intl.DateTimeFormat(undefined, {
        month: "long",
        year: "numeric"
    });
    const dayFormatter = new Intl.DateTimeFormat(undefined, {
        dateStyle: "full"
    });

    const isSameDate = (left, right) =>
        left.getFullYear() === right.getFullYear() &&
        left.getMonth() === right.getMonth() &&
        left.getDate() === right.getDate();

    const renderCalendar = () => {
        monthHeading.textContent = monthFormatter.format(displayedMonth);
        grid.replaceChildren();

        const firstGridDate = new Date(
            displayedMonth.getFullYear(),
            displayedMonth.getMonth(),
            1 - displayedMonth.getDay());

        for (let offset = 0; offset < 42; offset += 1) {
            const date = new Date(firstGridDate);
            date.setDate(firstGridDate.getDate() + offset);

            const dayButton = document.createElement("button");
            dayButton.type = "button";
            dayButton.className = "availability-calendar-day";
            dayButton.textContent = date.getDate().toString();
            dayButton.setAttribute("role", "gridcell");
            dayButton.setAttribute("aria-label", dayFormatter.format(date));

            const isOutsideMonth =
                date.getMonth() !== displayedMonth.getMonth();

            if (isOutsideMonth) {
                dayButton.classList.add("is-outside-month");
            }

            if (isSameDate(date, today)) {
                dayButton.classList.add("is-today");
            }

            if (isSameDate(date, selectedDate)) {
                dayButton.classList.add("is-selected");
                dayButton.setAttribute("aria-selected", "true");
            }

            if (date < minimumDate ||
                (currentMonthOnly && isOutsideMonth)) {
                dayButton.disabled = true;
            } else {
                dayButton.addEventListener("click", () => {
                    selectedDate = date;
                    valueInput.value = formatValue(date);

                    if (date.getMonth() !== displayedMonth.getMonth()) {
                        displayedMonth = new Date(
                            date.getFullYear(),
                            date.getMonth(),
                            1);
                    }

                    renderCalendar();
                });
            }

            grid.append(dayButton);
        }

        const minimumMonth = new Date(
            minimumDate.getFullYear(),
            minimumDate.getMonth(),
            1);
        previousButton.disabled = displayedMonth <= minimumMonth;
    };

    previousButton.addEventListener("click", () => {
        displayedMonth = new Date(
            displayedMonth.getFullYear(),
            displayedMonth.getMonth() - 1,
            1);
        renderCalendar();
    });

    nextButton.addEventListener("click", () => {
        displayedMonth = new Date(
            displayedMonth.getFullYear(),
            displayedMonth.getMonth() + 1,
            1);
        renderCalendar();
    });

    valueInput.value = formatValue(selectedDate);
    renderCalendar();

    const specificAvailabilityModal = document.querySelector(
        "[data-specific-availability-modal]");

    if (specificAvailabilityModal) {
        const specificForm = specificAvailabilityModal.querySelector(
            "[data-specific-availability-form]");
        const specificTimeInput = specificAvailabilityModal.querySelector(
            "[data-specific-slot-time]");
        const specificAddButton = specificAvailabilityModal.querySelector(
            "[data-specific-slot-add]");
        const specificSlotList = specificAvailabilityModal.querySelector(
            "[data-specific-slot-list]");
        const specificEmpty = specificAvailabilityModal.querySelector(
            "[data-specific-slot-empty]");
        const specificError = specificAvailabilityModal.querySelector(
            "[data-specific-slot-error]");
        const specificInputs = specificAvailabilityModal.querySelector(
            "[data-specific-time-inputs]");
        let specificTimes = [...specificInputs.querySelectorAll("input")]
            .map((input) => input.value)
            .filter(Boolean);

        const showSpecificError = (message) => {
            specificError.textContent = message;
            specificError.hidden = !message;
        };

        const formatSpecificTime = (minutes) => {
            const hours = Math.floor(minutes / 60) % 24;
            const minute = minutes % 60;
            const time = new Date(2000, 0, 1, hours, minute);
            return time.toLocaleTimeString(undefined, {
                hour: "numeric",
                minute: "2-digit"
            });
        };

        const renderSpecificTimes = () => {
            specificTimes = [...new Set(specificTimes)].sort();
            specificSlotList.replaceChildren();
            specificInputs.replaceChildren();
            specificEmpty.hidden = specificTimes.length > 0;

            specificTimes.forEach((time) => {
                const [hours, minutes] = time.split(":").map(Number);
                const startMinutes = hours * 60 + minutes;
                const endMinutes = startMinutes + 60;
                const hiddenInput = document.createElement("input");
                hiddenInput.type = "hidden";
                hiddenInput.name = "SpecificScheduleTimes";
                hiddenInput.value = time;
                specificInputs.append(hiddenInput);

                const slot = document.createElement("article");
                slot.className = "specific-availability-slot";
                const label = document.createElement("span");
                label.className = "recurring-editor-slot-time";
                label.textContent =
                    `${formatSpecificTime(startMinutes)} – ${formatSpecificTime(endMinutes)}`;
                const removeButton = document.createElement("button");
                removeButton.type = "button";
                removeButton.innerHTML = deleteTimeSlotIcon;
                removeButton.setAttribute(
                    "aria-label",
                    `Remove ${label.textContent}`);
                removeButton.addEventListener("click", () => {
                    specificTimes = specificTimes.filter((item) =>
                        item !== time);
                    renderSpecificTimes();
                });
                slot.append(label, removeButton);
                specificSlotList.append(slot);
            });
        };

        specificAddButton.addEventListener("click", () => {
            const time = specificTimeInput.value;

            if (!time) {
                showSpecificError("Choose a time before adding a slot.");
                return;
            }

            if (specificTimes.includes(time)) {
                showSpecificError("That time slot has already been added.");
                return;
            }

            const selectedDateValue = valueInput.value;
            const candidateMinutes = toScheduleMinute(
                selectedDateValue,
                time);
            const pendingMinutes = specificTimes.map((existingTime) =>
                toScheduleMinute(selectedDateValue, existingTime));

            if (overlapsSchedule(
                    candidateMinutes,
                    [...occupiedScheduleMinutes, ...pendingMinutes])) {
                showSpecificError(showOverlapWarning(
                    time,
                    specificAvailabilityModal));
                return;
            }

            if (specificTimes.length >= 15) {
                showSpecificError("You can add a maximum of 15 time slots.");
                return;
            }

            specificTimes.push(time);
            specificTimeInput.value = "";
            showSpecificError("");
            renderSpecificTimes();
        });

        specificForm.addEventListener("submit", (event) => {
            if (specificTimes.length === 0) {
                event.preventDefault();
                showSpecificError("Add at least one time slot.");
            }
        });

        renderSpecificTimes();

        if (specificAvailabilityModal.dataset.openOnLoad === "true" &&
            window.bootstrap?.Modal) {
            const modal = new window.bootstrap.Modal(
                specificAvailabilityModal);
            modal.show();
        }
    }

    const recurringSchedule = document.querySelector(
        "[data-recurring-schedule]");

    if (recurringSchedule) {
        const startInput = recurringSchedule.querySelector(
            "[data-recurring-start]");
        const endInput = recurringSchedule.querySelector(
            "[data-recurring-end]");
        const addButton = recurringSchedule.querySelector(
            "[data-recurring-add]");
        const previousWeekButton = recurringSchedule.querySelector(
            "[data-recurring-previous-week]");
        const nextWeekButton = recurringSchedule.querySelector(
            "[data-recurring-next-week]");
        const weekLabel = recurringSchedule.querySelector(
            "[data-recurring-week-label]");
        const rangesContainer = recurringSchedule.querySelector(
            "[data-recurring-ranges]");
        const emptyState = recurringSchedule.querySelector(
            "[data-recurring-empty]");
        const errorMessage = recurringSchedule.querySelector(
            "[data-recurring-error]");
        const rangesByWeek = new Map();

        const currentDate = new Date();
        currentDate.setHours(0, 0, 0, 0);
        const mondayOffset = (currentDate.getDay() + 6) % 7;
        let selectedWeekStart = new Date(currentDate);
        selectedWeekStart.setDate(
            selectedWeekStart.getDate() - mondayOffset);

        const weekKey = (date) => formatValue(date);

        const formatWeekLabel = (weekStart) => {
            const weekEnd = new Date(weekStart);
            weekEnd.setDate(weekEnd.getDate() + 5);
            const startMonth = weekStart.toLocaleDateString(undefined, {
                month: "short"
            });
            const endMonth = weekEnd.toLocaleDateString(undefined, {
                month: "short"
            });

            if (weekStart.getMonth() === weekEnd.getMonth()) {
                return `${startMonth} ${weekStart.getDate()} – ${weekEnd.getDate()}`;
            }

            return `${startMonth} ${weekStart.getDate()} – ${endMonth} ${weekEnd.getDate()}`;
        };

        const formatRangeTime = (value) => {
            const [hours, minutes] = value.split(":").map(Number);
            const time = new Date(2000, 0, 1, hours, minutes);
            return time.toLocaleTimeString(undefined, {
                hour: "numeric",
                minute: "2-digit"
            });
        };

        const showRangeError = (message) => {
            errorMessage.textContent = message;
            errorMessage.hidden = !message;
        };

        const renderRanges = () => {
            const key = weekKey(selectedWeekStart);
            const ranges = [...(rangesByWeek.get(key) ?? [])]
                .sort((left, right) => left.start.localeCompare(right.start));

            weekLabel.textContent = formatWeekLabel(selectedWeekStart);
            rangesContainer.replaceChildren();
            emptyState.hidden = ranges.length > 0;

            ranges.forEach((range) => {
                const rangeItem = document.createElement("article");
                rangeItem.className = "recurring-schedule-range";

                const timeText = document.createElement("span");
                timeText.textContent =
                    `${formatRangeTime(range.start)} – ${formatRangeTime(range.end)}`;

                const removeButton = document.createElement("button");
                removeButton.type = "button";
                removeButton.setAttribute(
                    "aria-label",
                    `Remove ${timeText.textContent}`);
                removeButton.innerHTML = deleteTimeSlotIcon;
                removeButton.addEventListener("click", () => {
                    const storedRanges = rangesByWeek.get(key) ?? [];
                    rangesByWeek.set(
                        key,
                        storedRanges.filter((item) =>
                            item.start !== range.start || item.end !== range.end));
                    renderRanges();
                });

                rangeItem.append(timeText, removeButton);
                rangesContainer.append(rangeItem);
            });
        };

        addButton.addEventListener("click", () => {
            const start = startInput.value;
            const end = endInput.value;

            if (!start || !end) {
                showRangeError("Choose both a start and end time.");
                return;
            }

            if (start >= end) {
                showRangeError("End time must be later than start time.");
                return;
            }

            const key = weekKey(selectedWeekStart);
            const ranges = rangesByWeek.get(key) ?? [];
            const isDuplicate = ranges.some((range) =>
                range.start === start && range.end === end);

            if (isDuplicate) {
                showRangeError("That time range has already been added.");
                return;
            }

            rangesByWeek.set(key, [...ranges, { start, end }]);
            startInput.value = "";
            endInput.value = "";
            showRangeError("");
            renderRanges();
        });

        previousWeekButton.addEventListener("click", () => {
            selectedWeekStart.setDate(selectedWeekStart.getDate() - 7);
            showRangeError("");
            renderRanges();
        });

        nextWeekButton.addEventListener("click", () => {
            selectedWeekStart.setDate(selectedWeekStart.getDate() + 7);
            showRangeError("");
            renderRanges();
        });

        renderRanges();
    }

    const recurringEditor = document.querySelector(
        "[data-recurring-editor]");

    if (recurringEditor) {
        const recurringCalendar = recurringEditor.querySelector(
            "[data-recurring-calendar]");
        const recurringCalendarGrid = recurringEditor.querySelector(
            "[data-recurring-calendar-grid]");
        const recurringCalendarMonth = recurringEditor.querySelector(
            "[data-recurring-calendar-month]");
        const recurringCalendarPrevious = recurringEditor.querySelector(
            "[data-recurring-calendar-previous]");
        const recurringCalendarNext = recurringEditor.querySelector(
            "[data-recurring-calendar-next]");
        const selectionText = recurringEditor.querySelector(
            "[data-recurring-selection]");
        const selectedInputs = recurringEditor.querySelector(
            "[data-recurring-selected-inputs]");
        const slotTimeInput = recurringEditor.querySelector(
            "[data-recurring-slot-time]");
        const addSlotButton = recurringEditor.querySelector(
            "[data-recurring-slot-add]");
        const timeInputs = recurringEditor.querySelector(
            "[data-recurring-time-inputs]");
        const slotList = recurringEditor.querySelector(
            "[data-recurring-slot-list]");
        const emptySlots = recurringEditor.querySelector(
            "[data-recurring-slot-empty]");
        const editorError = recurringEditor.querySelector(
            "[data-recurring-editor-error]");
        const createButton = recurringEditor.querySelector(
            "[data-recurring-create]");
        const progressModal = document.querySelector(
            "[data-recurring-progress]");
        const selectedDates = new Set(
            Array.from(selectedInputs.querySelectorAll("input"))
                .map((input) => input.value));
        const recurringMinimumDate = parseDate(
            recurringCalendar.dataset.minDate);
        let recurringDisplayedMonth = new Date(
            recurringMinimumDate.getFullYear(),
            recurringMinimumDate.getMonth(),
            1);
        let scheduleTimes = Array.from(
            timeInputs.querySelectorAll("input"))
            .map((input) => input.value)
            .filter(Boolean)
            .sort();

        const showEditorError = (message) => {
            editorError.textContent = message;
            editorError.hidden = !message;
        };

        const joinSelectedDates = (labels) => {
            if (labels.length === 1) {
                return labels[0];
            }

            if (labels.length === 2) {
                return `${labels[0]} and ${labels[1]}`;
            }

            return `${labels.slice(0, -1).join(", ")} and ${labels.at(-1)}`;
        };

        const syncSelectedDates = () => {
            selectedInputs.replaceChildren();
            const orderedDates = [...selectedDates].sort();
            orderedDates.forEach((date) => {
                const input = document.createElement("input");
                input.type = "hidden";
                input.name = "SelectedDates";
                input.value = date;
                selectedInputs.append(input);
            });

            if (orderedDates.length === 0) {
                selectionText.textContent = "";
                selectionText.hidden = true;
                return;
            }

            selectionText.hidden = false;
            const labels = orderedDates.map((date) =>
                parseDate(date).toLocaleDateString(undefined, {
                    day: "numeric",
                    month: "long"
                }));
            selectionText.replaceChildren();
            const strong = document.createElement("strong");
            strong.textContent = labels.length <= 3
                ? joinSelectedDates(labels)
                : `${labels.slice(0, 3).join(", ")} and ${labels.length - 3} more`;
            selectionText.append(strong);
        };

        const renderRecurringCalendar = () => {
            recurringCalendarMonth.textContent =
                monthFormatter.format(recurringDisplayedMonth);
            recurringCalendarGrid.replaceChildren();

            const firstGridDate = new Date(
                recurringDisplayedMonth.getFullYear(),
                recurringDisplayedMonth.getMonth(),
                1 - recurringDisplayedMonth.getDay());

            for (let offset = 0; offset < 42; offset += 1) {
                const date = new Date(firstGridDate);
                date.setDate(firstGridDate.getDate() + offset);
                const dateValue = formatValue(date);
                const dayButton = document.createElement("button");
                dayButton.type = "button";
                dayButton.className = "availability-calendar-day";
                dayButton.textContent = date.getDate().toString();
                dayButton.setAttribute("role", "gridcell");
                dayButton.setAttribute(
                    "aria-label",
                    dayFormatter.format(date));

                if (date.getMonth() !==
                    recurringDisplayedMonth.getMonth()) {
                    dayButton.classList.add("is-outside-month");
                }

                if (isSameDate(date, today)) {
                    dayButton.classList.add("is-today");
                }

                if (selectedDates.has(dateValue)) {
                    dayButton.classList.add("is-selected");
                    dayButton.setAttribute("aria-selected", "true");
                } else {
                    dayButton.setAttribute("aria-selected", "false");
                }

                if (date < recurringMinimumDate) {
                    dayButton.disabled = true;
                } else {
                    dayButton.addEventListener("click", () => {
                        if (selectedDates.has(dateValue)) {
                            selectedDates.delete(dateValue);
                        } else {
                            selectedDates.add(dateValue);
                        }

                        if (date.getMonth() !==
                            recurringDisplayedMonth.getMonth()) {
                            recurringDisplayedMonth = new Date(
                                date.getFullYear(),
                                date.getMonth(),
                                1);
                        }

                        showEditorError("");
                        syncSelectedDates();
                        renderRecurringCalendar();
                    });
                }

                recurringCalendarGrid.append(dayButton);
            }

            const minimumMonth = new Date(
                recurringMinimumDate.getFullYear(),
                recurringMinimumDate.getMonth(),
                1);
            recurringCalendarPrevious.disabled =
                recurringDisplayedMonth <= minimumMonth;
        };

        recurringCalendarPrevious.addEventListener("click", () => {
            recurringDisplayedMonth = new Date(
                recurringDisplayedMonth.getFullYear(),
                recurringDisplayedMonth.getMonth() - 1,
                1);
            renderRecurringCalendar();
        });

        recurringCalendarNext.addEventListener("click", () => {
            recurringDisplayedMonth = new Date(
                recurringDisplayedMonth.getFullYear(),
                recurringDisplayedMonth.getMonth() + 1,
                1);
            renderRecurringCalendar();
        });

        const formatSlotTime = (minutes) => {
            const hours = Math.floor(minutes / 60) % 24;
            const minute = minutes % 60;
            const time = new Date(2000, 0, 1, hours, minute);
            return time.toLocaleTimeString(undefined, {
                hour: "numeric",
                minute: "2-digit"
            });
        };

        const renderScheduleTimes = () => {
            scheduleTimes = [...new Set(scheduleTimes)].sort();
            slotList.replaceChildren();
            timeInputs.replaceChildren();
            emptySlots.hidden = true;

            scheduleTimes.forEach((time) => {
                const [hours, minutes] = time.split(":").map(Number);
                const startMinutes = hours * 60 + minutes;
                const endMinutes = startMinutes + 60;

                const hiddenInput = document.createElement("input");
                hiddenInput.type = "hidden";
                hiddenInput.name = "ScheduleTimes";
                hiddenInput.value = time;
                timeInputs.append(hiddenInput);

                const slot = document.createElement("article");
                slot.className = "recurring-editor-slot";
                const label = document.createElement("span");
                label.className = "recurring-editor-slot-time";
                label.textContent =
                    `${formatSlotTime(startMinutes)} – ${formatSlotTime(endMinutes)}`;
                const removeButton = document.createElement("button");
                removeButton.type = "button";
                removeButton.setAttribute(
                    "aria-label",
                    `Remove ${label.textContent}`);
                removeButton.innerHTML = deleteTimeSlotIcon;
                removeButton.addEventListener("click", () => {
                    scheduleTimes = scheduleTimes.filter((item) =>
                        item !== time);
                    renderScheduleTimes();
                });

                slot.append(label, removeButton);
                slotList.append(slot);
            });
        };

        addSlotButton.addEventListener("click", () => {
            const time = slotTimeInput.value;

            if (!time) {
                showEditorError("Choose a time before adding a slot.");
                return;
            }

            if (scheduleTimes.includes(time)) {
                showEditorError("That time slot has already been added.");
                return;
            }

            const selectedDateValues = [...selectedDates];
            const candidateMinutes = selectedDateValues.map((dateValue) =>
                toScheduleMinute(dateValue, time));
            const pendingMinutes = selectedDateValues.flatMap((dateValue) =>
                scheduleTimes.map((existingTime) =>
                    toScheduleMinute(dateValue, existingTime)));
            const candidateHasConflict = candidateMinutes.length > 0
                ? candidateMinutes.some((minutes) =>
                    overlapsSchedule(
                        minutes,
                        [...occupiedScheduleMinutes, ...pendingMinutes]))
                : scheduleTimes.some((existingTime) => {
                    const [candidateHours, candidateMinute] = time
                        .split(":")
                        .map(Number);
                    const [existingHours, existingMinute] = existingTime
                        .split(":")
                        .map(Number);
                    return Math.abs(
                        candidateHours * 60 + candidateMinute -
                        (existingHours * 60 + existingMinute)) < 75;
                });

            if (candidateHasConflict) {
                showEditorError(showOverlapWarning(time));
                return;
            }

            if (scheduleTimes.length >= 15) {
                showEditorError(
                    "A recurring schedule can contain a maximum of 15 time slots.");
                return;
            }

            scheduleTimes.push(time);
            slotTimeInput.value = "";
            showEditorError("");
            renderScheduleTimes();
        });

        recurringEditor.addEventListener("submit", (event) => {
            if (selectedDates.size === 0) {
                event.preventDefault();
                showEditorError("Select at least one schedule date.");
                return;
            }

            if (scheduleTimes.length === 0) {
                event.preventDefault();
                showEditorError("Add at least one time slot.");
                return;
            }

            createButton.disabled = true;
            recurringEditor.setAttribute("aria-busy", "true");
            progressModal.hidden = false;
        });

        syncSelectedDates();
        renderRecurringCalendar();
        renderScheduleTimes();
    }

    const availabilityChart = document.querySelector(
        "[data-availability-summary-chart]");

    if (availabilityChart && window.Chart) {
        const parseAvailabilityValues = (values) =>
            (values ?? "")
                .split(",")
                .map((value) => Number(value) || 0);
        const parseAvailabilityLabels = (labels) =>
            (labels ?? "").split("|").filter(Boolean);
        const dayValues = parseAvailabilityValues(
            availabilityChart.dataset.dayValues);
        const dayLabels = parseAvailabilityLabels(
            availabilityChart.dataset.dayLabels)
            .map((label) => label.split(" ")[0]);
        const weekValues = parseAvailabilityValues(
            availabilityChart.dataset.weekValues);
        const weekLabels = parseAvailabilityLabels(
            availabilityChart.dataset.weekLabels);
        const chartViewButtons = Array.from(document.querySelectorAll(
            "[data-availability-chart-view]"));
        const dayBarColors = [
            "#ad0151",
            "#4ac1c1",
            "#713b72",
            "#35658a",
            "#6f6f6f",
            "#8e0043",
            "#2f7477"
        ];
        const dayBorderColors = [
            "#8e0043",
            "#306f71",
            "#583059",
            "#284f6d",
            "#555555",
            "#720036",
            "#245b5d"
        ];
        const weekBarColors = [
            "#ad0151",
            "#3e8f91",
            "#713b72",
            "#6f6f6f"
        ];
        const weekBorderColors = [
            "#8e0043",
            "#306f71",
            "#583059",
            "#555555"
        ];
        let activeAvailabilityValues = dayValues;

        const availabilitySummaryChart = new window.Chart(availabilityChart, {
            type: "bar",
            data: {
                labels: dayLabels,
                datasets: [{
                    label: "Open slots",
                    data: dayValues,
                    backgroundColor: dayBarColors,
                    borderColor: dayBorderColors,
                    borderRadius: 7,
                    borderSkipped: false,
                    borderWidth: 0,
                    hoverBorderWidth: 0,
                    maxBarThickness: 42
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: {
                    duration: 350
                },
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: (context) => {
                                const value = context.parsed.y;
                                return `${value} open ${value === 1 ? "slot" : "slots"}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        border: {
                            display: false
                        },
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: "#59616d",
                            font: {
                                size: 12,
                                weight: "600"
                            },
                            padding: 10
                        },
                        title: {
                            color: "#747b86",
                            display: true,
                            font: {
                                size: 12,
                                weight: "600"
                            },
                            padding: {
                                top: 18
                            },
                            text: `Next 7 days • ${availabilityChart.dataset.dayRange ?? ""}`
                        }
                    },
                    y: {
                        beginAtZero: true,
                        border: {
                            display: false
                        },
                        grid: {
                            color: "#edf0f2"
                        },
                        suggestedMax: Math.max(...activeAvailabilityValues, 1),
                        ticks: {
                            color: "#747b86",
                            maxTicksLimit: 6,
                            precision: 0
                        },
                        title: {
                            color: "#59616d",
                            display: true,
                            font: {
                                size: 12,
                                weight: "600"
                            },
                            text: "Open slots"
                        }
                    }
                }
            }
        });

        const setAvailabilityChartView = (view) => {
            const showWeeks = view === "weeks";
            activeAvailabilityValues = showWeeks ? weekValues : dayValues;
            availabilitySummaryChart.data.labels = showWeeks
                ? weekLabels
                : dayLabels;
            availabilitySummaryChart.data.datasets[0].data =
                activeAvailabilityValues;
            availabilitySummaryChart.data.datasets[0].backgroundColor =
                showWeeks ? weekBarColors : dayBarColors;
            availabilitySummaryChart.data.datasets[0].borderColor =
                showWeeks ? weekBorderColors : dayBorderColors;
            availabilitySummaryChart.options.scales.x.title.text = showWeeks
                ? `Next 4 weeks • ${availabilityChart.dataset.weekRange ?? ""}`
                : `Next 7 days • ${availabilityChart.dataset.dayRange ?? ""}`;
            availabilitySummaryChart.options.scales.y.suggestedMax =
                Math.max(...activeAvailabilityValues, 1);
            availabilityChart.setAttribute(
                "aria-label",
                showWeeks
                    ? "Open availability totals for the next four weeks"
                    : "Open availability for the next seven days");

            chartViewButtons.forEach((button) => {
                const isActive =
                    button.dataset.availabilityChartView === view;
                button.classList.toggle("is-active", isActive);
                button.setAttribute("aria-pressed", String(isActive));
            });

            availabilitySummaryChart.update();
        };

        chartViewButtons.forEach((button) => {
            button.addEventListener("click", () => {
                setAvailabilityChartView(
                    button.dataset.availabilityChartView ?? "days");
            });
        });
    }

    const deleteAvailabilityId = document.querySelector(
        "[data-availability-delete-id]");
    const deleteAvailabilityLabel = document.querySelector(
        "[data-availability-delete-label]");
    const deleteAvailabilityTriggers = document.querySelectorAll(
        "[data-availability-delete-trigger]");

    deleteAvailabilityTriggers.forEach((trigger) => {
        trigger.addEventListener("click", () => {
            if (deleteAvailabilityId) {
                deleteAvailabilityId.value =
                    trigger.dataset.availabilityId ?? "";
            }

            if (deleteAvailabilityLabel) {
                deleteAvailabilityLabel.textContent =
                    trigger.dataset.availabilityLabel ?? "this time slot";
            }
        });
    });

    const selectAllAvailability = document.querySelector(
        "[data-availability-select-all]");
    const availabilityRowSelections = Array.from(
        document.querySelectorAll("[data-availability-select-row]"));
    const bulkAvailabilityActions = document.querySelector(
        "[data-availability-bulk-actions]");
    const availabilitySelectionCount = document.querySelector(
        "[data-availability-selection-count]");
    const bulkDeleteTrigger = document.querySelector(
        "[data-availability-bulk-delete-trigger]");
    const bulkDeleteCount = document.querySelector(
        "[data-availability-bulk-delete-count]");
    const bulkDeleteInputs = document.querySelector(
        "[data-availability-bulk-delete-inputs]");

    const getSelectedAvailabilityRows = () =>
        availabilityRowSelections.filter((checkbox) => checkbox.checked);

    const updateAvailabilitySelection = () => {
        const selectedCount = getSelectedAvailabilityRows().length;
        const allRowsSelected = availabilityRowSelections.length > 0 &&
            selectedCount === availabilityRowSelections.length;

        if (selectAllAvailability) {
            selectAllAvailability.checked = allRowsSelected;
            selectAllAvailability.indeterminate = false;
        }

        if (bulkAvailabilityActions) {
            bulkAvailabilityActions.hidden = selectedCount === 0;
        }

        if (availabilitySelectionCount) {
            availabilitySelectionCount.textContent =
                `${selectedCount} selected`;
        }
    };

    selectAllAvailability?.addEventListener("change", () => {
        availabilityRowSelections.forEach((checkbox) => {
            checkbox.checked = selectAllAvailability.checked;
        });
        updateAvailabilitySelection();
    });

    availabilityRowSelections.forEach((checkbox) => {
        checkbox.addEventListener("change", updateAvailabilitySelection);
    });

    bulkDeleteTrigger?.addEventListener("click", () => {
        const selectedRows = getSelectedAvailabilityRows();

        if (bulkDeleteCount) {
            bulkDeleteCount.textContent = selectedRows.length === 1
                ? "1 availability slot"
                : `${selectedRows.length} availability slots`;
        }

        if (bulkDeleteInputs) {
            bulkDeleteInputs.replaceChildren(...selectedRows.map((checkbox) => {
                const input = document.createElement("input");
                input.type = "hidden";
                input.name = "availabilityIds";
                input.value = checkbox.value;
                return input;
            }));
        }
    });

    const editAvailabilityModal = document.querySelector(
        "[data-availability-edit-modal]");
    const editAvailabilityId = document.querySelector(
        "[data-availability-edit-id]");
    const editAvailabilityDate = document.querySelector(
        "[data-availability-edit-date]");
    const editAvailabilityTime = document.querySelector(
        "[data-availability-edit-time]");
    const editAvailabilityTriggers = document.querySelectorAll(
        "[data-availability-edit-trigger]");

    editAvailabilityTriggers.forEach((trigger) => {
        trigger.addEventListener("click", () => {
            if (editAvailabilityId) {
                editAvailabilityId.value =
                    trigger.dataset.availabilityId ?? "";
            }

            if (editAvailabilityDate) {
                editAvailabilityDate.textContent =
                    trigger.dataset.availabilityDate ?? "";
            }

            if (editAvailabilityTime) {
                editAvailabilityTime.value =
                    trigger.dataset.availabilityTime ?? "";
            }
        });
    });

    if (editAvailabilityModal?.dataset.openOnLoad === "true" &&
        window.bootstrap?.Modal) {
        const editModal = new window.bootstrap.Modal(
            editAvailabilityModal);
        editModal.show();
    }

})();
