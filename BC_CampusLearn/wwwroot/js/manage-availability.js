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
                const label = document.createElement("strong");
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
        const dateButtons = Array.from(
            recurringEditor.querySelectorAll("[data-recurring-date]"));
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
            const selectedButtons = dateButtons.filter((button) =>
                selectedDates.has(button.dataset.date));

            dateButtons.forEach((button) => {
                const isSelected = selectedDates.has(button.dataset.date);
                button.classList.toggle("is-selected", isSelected);
                button.setAttribute("aria-pressed", isSelected.toString());
            });

            selectedInputs.replaceChildren();
            selectedButtons.forEach((button) => {
                const input = document.createElement("input");
                input.type = "hidden";
                input.name = "SelectedDates";
                input.value = button.dataset.date;
                selectedInputs.append(input);
            });

            if (selectedButtons.length === 0) {
                selectionText.textContent =
                    "Select the dates for your recurring schedule.";
                return;
            }

            const labels = selectedButtons.map((button) =>
                button.dataset.dateLabel);
            selectionText.replaceChildren();
            selectionText.append("Recurring schedule for: ");
            const strong = document.createElement("strong");
            strong.textContent = joinSelectedDates(labels);
            selectionText.append(strong);
        };

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
            emptySlots.hidden = scheduleTimes.length > 0;

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
                const label = document.createElement("strong");
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

        dateButtons.forEach((button) => {
            button.addEventListener("click", () => {
                const date = button.dataset.date;

                if (selectedDates.has(date)) {
                    selectedDates.delete(date);
                } else {
                    selectedDates.add(date);
                }

                showEditorError("");
                syncSelectedDates();
            });
        });

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
        renderScheduleTimes();
    }

    const availabilityChart = document.querySelector(
        "[data-availability-summary-chart]");

    if (availabilityChart && window.Chart) {
        const availabilityValues = [
            Number(availabilityChart.dataset.today) || 0,
            Number(availabilityChart.dataset.sevenDays) || 0,
            Number(availabilityChart.dataset.thirtyOneDays) || 0
        ];

        new window.Chart(availabilityChart, {
            type: "bar",
            data: {
                labels: ["Today", "7 Days", "31 Days"],
                datasets: [{
                    label: "Open availability",
                    data: availabilityValues,
                    backgroundColor: [
                        "#ad0151",
                        "#21b8bd",
                        "#f2a93b"
                    ],
                    borderColor: [
                        "#ad0151",
                        "#159da2",
                        "#c78000"
                    ],
                    borderRadius: 6,
                    borderSkipped: false,
                    borderWidth: 1,
                    maxBarThickness: 58
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
                        displayColors: false,
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
                                size: 11,
                                weight: "600"
                            }
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
                        suggestedMax: Math.max(...availabilityValues, 1),
                        ticks: {
                            color: "#747b86",
                            maxTicksLimit: 6,
                            precision: 0
                        },
                        title: {
                            color: "#59616d",
                            display: true,
                            font: {
                                size: 11,
                                weight: "600"
                            },
                            text: "Open availability"
                        }
                    }
                }
            }
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

    const successModalElement = document.querySelector(
        "[data-availability-success-modal]");

    if (successModalElement && window.bootstrap?.Modal) {
        const successModal = new window.bootstrap.Modal(
            successModalElement);
        successModal.show();
    }
})();
