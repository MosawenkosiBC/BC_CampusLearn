(() => {
    const setupCountdown = () => {
        const countdown =
            document.querySelector("[data-session-countdown]");

        if (!countdown) {
            return;
        }

        const sessionStart =
            new Date(countdown.dataset.sessionStart);

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
                return `${pluralize(
                    totalMinutes,
                    "min",
                    "mins")} left`;
            }

            const totalHours = Math.floor(totalMinutes / 60);
            const minutes = totalMinutes % 60;

            if (totalHours < 24) {
                const hoursLabel = pluralize(
                    totalHours,
                    "hr",
                    "hrs");

                return minutes === 0
                    ? `${hoursLabel} left`
                    : `${hoursLabel} ${pluralize(
                        minutes,
                        "min",
                        "mins")} left`;
            }

            const days = Math.floor(totalHours / 24);
            const hours = totalHours % 24;
            const daysLabel = pluralize(
                days,
                "day",
                "days");

            return hours === 0
                ? `${daysLabel} left`
                : `${daysLabel} ${pluralize(
                    hours,
                    "hr",
                    "hrs")} left`;
        };

        const updateCountdown = () => {
            countdown.textContent = formatCountdown(
                sessionStart.getTime() - Date.now());
        };

        updateCountdown();
        window.setInterval(updateCountdown, 30000);
    };

    const setupBookingPopover = () => {
        const popover =
            document.querySelector("[data-booking-popover]");

        if (!popover) {
            return;
        }

        const closeButton = popover.querySelector(
            "[data-booking-popover-close]");
        const bookingIdInput = popover.querySelector(
            "[data-booking-id-input]");
        const studentName = popover.querySelector(
            "[data-booking-student]");
        const module = popover.querySelector(
            "[data-booking-module]");
        const time = popover.querySelector(
            "[data-booking-time]");
        const location = popover.querySelector(
            "[data-booking-location]");

        let activeTrigger = null;
        let closeTimer = null;

        const positionPopover = () => {
            if (!activeTrigger || popover.hidden) {
                return;
            }

            const triggerRect =
                activeTrigger.getBoundingClientRect();
            const popoverRect =
                popover.getBoundingClientRect();
            const gap = 8;
            const edge = 12;

            let left = triggerRect.left +
                (triggerRect.width - popoverRect.width) / 2;
            left = Math.max(
                edge,
                Math.min(
                    left,
                    window.innerWidth -
                        popoverRect.width -
                        edge));

            let top = triggerRect.bottom + gap;

            if (top + popoverRect.height >
                window.innerHeight - edge) {
                top = triggerRect.top -
                    popoverRect.height -
                    gap;
            }

            top = Math.max(edge, top);

            popover.style.left = `${left}px`;
            popover.style.top = `${top}px`;
        };

        const cancelScheduledClose = () => {
            if (closeTimer !== null) {
                window.clearTimeout(closeTimer);
                closeTimer = null;
            }
        };

        const closePopover = (restoreFocus = false) => {
            cancelScheduledClose();

            if (activeTrigger) {
                activeTrigger.setAttribute(
                    "aria-expanded",
                    "false");

                if (restoreFocus) {
                    activeTrigger.focus();
                }
            }

            activeTrigger = null;
            popover.hidden = true;
        };

        const scheduleClose = () => {
            cancelScheduledClose();
            closeTimer = window.setTimeout(
                () => closePopover(),
                220);
        };

        const openPopover = (trigger) => {
            cancelScheduledClose();

            if (activeTrigger &&
                activeTrigger !== trigger) {
                activeTrigger.setAttribute(
                    "aria-expanded",
                    "false");
            }

            activeTrigger = trigger;
            trigger.setAttribute("aria-expanded", "true");

            const name =
                trigger.dataset.bookingStudent || "Student";

            bookingIdInput.value =
                trigger.dataset.bookingId || "";
            studentName.textContent = name;
            module.textContent =
                trigger.dataset.bookingModule || "Not provided";
            time.textContent =
                trigger.dataset.bookingTime || "Not provided";
            location.textContent =
                trigger.dataset.bookingLocation || "Not provided";

            popover.hidden = false;
            positionPopover();
        };

        const getTrigger = (target) =>
            target instanceof Element
                ? target.closest(
                    "[data-booking-popover-trigger]")
                : null;

        document.addEventListener("mouseover", (event) => {
            const trigger = getTrigger(event.target);

            if (trigger &&
                !trigger.contains(event.relatedTarget)) {
                openPopover(trigger);
            }
        });

        document.addEventListener("mouseout", (event) => {
            const trigger = getTrigger(event.target);

            if (trigger &&
                !trigger.contains(event.relatedTarget)) {
                scheduleClose();
            }
        });

        document.addEventListener("focusin", (event) => {
            const trigger = getTrigger(event.target);

            if (trigger) {
                openPopover(trigger);
            }
        });

        document.addEventListener("focusout", (event) => {
            if (getTrigger(event.target)) {
                scheduleClose();
            }
        });

        document.addEventListener("click", (event) => {
            const trigger = getTrigger(event.target);

            if (!trigger) {
                return;
            }

            if (activeTrigger === trigger &&
                !popover.hidden) {
                cancelScheduledClose();
                return;
            }

            openPopover(trigger);
        });

        popover.addEventListener(
            "mouseenter",
            cancelScheduledClose);
        popover.addEventListener(
            "mouseleave",
            scheduleClose);

        closeButton?.addEventListener(
            "click",
            () => closePopover(true));

        document.addEventListener("pointerdown", (event) => {
            if (popover.hidden ||
                popover.contains(event.target) ||
                activeTrigger?.contains(event.target)) {
                return;
            }

            closePopover();
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape" &&
                !popover.hidden) {
                closePopover(true);
            }
        });

        window.addEventListener(
            "resize",
            positionPopover);

        document.addEventListener(
            "scroll",
            () => closePopover(),
            true);
    };

    const setupAvailabilityViews = () => {
        const viewOptions = Array.from(
            document.querySelectorAll(
                "[data-availability-view-option]"));
        const viewLabel = document.querySelector(
            "[data-availability-view-label]");
        const rangeLabel = document.querySelector(
            "[data-availability-range-current]");
        const viewTitle = document.querySelector(
            "[data-availability-title]");
        const calendarElement = document.querySelector(
            "#tutor-availability-calendar");
        const eventDataElement = document.querySelector(
            "#tutor-availability-events");
        const viewTitles = {
            weekly: "Weekly Availability",
            monthly: "Monthly Availability",
            daily: "Daily Availability"
        };
        const fullCalendarViews = {
            weekly: "timeGridSevenDay",
            monthly: "dayGridMonth",
            daily: "timeGridDay"
        };
        const dailyStartHour = Math.max(
            0,
            new Date().getHours() - 2);
        const dailyStartTime = `${String(dailyStartHour)
            .padStart(2, "0")}:00:00`;
        let selectedView = "weekly";

        if (viewOptions.length === 0 ||
            !calendarElement ||
            !eventDataElement ||
            !window.FullCalendar) {
            return;
        }

        let events = [];

        try {
            events = JSON.parse(eventDataElement.textContent);
        } catch {
            return;
        }

        const formatDate = (date, options) =>
            new Intl.DateTimeFormat("en-GB", options)
                .format(date);

        const updateRangeLabel = (view) => {
            if (!rangeLabel) {
                return;
            }

            if (view.type === "dayGridMonth") {
                rangeLabel.textContent = formatDate(
                    view.currentStart,
                    { month: "long", year: "numeric" });
                return;
            }

            if (view.type === "timeGridDay") {
                rangeLabel.textContent = formatDate(
                    view.currentStart,
                    {
                        day: "2-digit",
                        month: "long",
                        year: "numeric"
                    });
                return;
            }

            const inclusiveEnd = new Date(view.currentEnd);
            inclusiveEnd.setDate(inclusiveEnd.getDate() - 1);
            rangeLabel.textContent = `${formatDate(
                view.currentStart,
                { day: "2-digit", month: "short" })} – ${formatDate(
                inclusiveEnd,
                { day: "2-digit", month: "short" })}`;
        };

        const buildEventContent = (argument) => {
            const properties = argument.event.extendedProps;
            const isBooked = properties.status === "booked" &&
                properties.bookingId;
            const slot = document.createElement(
                isBooked ? "button" : "span");
            slot.className =
                `tutor-slot-tab is-${properties.status}`;
            slot.title = isBooked
                ? "Manage pending booking"
                : properties.statusLabel;

            if (isBooked) {
                slot.type = "button";
                slot.setAttribute(
                    "aria-label",
                    `Manage ${properties.studentName || "student"}'s pending booking`);
                slot.setAttribute("aria-haspopup", "dialog");
                slot.setAttribute("aria-expanded", "false");
                slot.setAttribute(
                    "aria-controls",
                    "booking-status-popover");
                slot.dataset.bookingPopoverTrigger = "";
                slot.dataset.bookingId = properties.bookingId;
                slot.dataset.bookingStudent =
                    properties.studentName || "Student";
                slot.dataset.bookingModule =
                    properties.module || "Not provided";
                slot.dataset.bookingTime =
                    properties.bookingTime || "Not provided";
                slot.dataset.bookingLocation =
                    properties.location || "Not provided";
            }

            const accessibleLabel = document.createElement("span");
            accessibleLabel.className = "visually-hidden";
            accessibleLabel.textContent = properties.statusLabel;
            slot.append(accessibleLabel);

            return { domNodes: [slot] };
        };

        const calendar = new FullCalendar.Calendar(
            calendarElement,
            {
                initialView: "timeGridSevenDay",
                initialDate: calendarElement.dataset.initialDate,
                headerToolbar: false,
                firstDay: 1,
                height: "100%",
                allDaySlot: false,
                nowIndicator: true,
                expandRows: true,
                editable: false,
                selectable: false,
                eventStartEditable: false,
                eventDurationEditable: false,
                displayEventTime: false,
                slotDuration: "01:00:00",
                scrollTime: `${String(
                    Math.max(0, new Date().getHours() - 2))
                    .padStart(2, "0")}:00:00`,
                dayHeaderFormat: {
                    weekday: "short",
                    day: "2-digit"
                },
                dayHeaderContent: (argument) => {
                    const wrapper = document.createElement("span");
                    const weekday = document.createElement("strong");
                    weekday.textContent = argument.isToday
                        ? "Today"
                        : formatDate(
                            argument.date,
                            { weekday: "short" });
                    wrapper.append(weekday);

                    if (argument.view.type !== "dayGridMonth") {
                        const day = document.createElement("span");
                        day.textContent = formatDate(
                            argument.date,
                            { day: "2-digit" });
                        wrapper.append(day);
                    }

                    return { domNodes: [wrapper] };
                },
                viewClass: "tutor-fullcalendar-view",
                dayHeaderClass: (argument) =>
                    `tutor-fc-day-header${
                        argument.isToday ? " is-today" : ""}`,
                dayHeaderInnerClass:
                    "tutor-fc-day-header-inner",
                dayHeaderDividerClass:
                    "tutor-fc-day-header-divider",
                slotHeaderDividerClass:
                    "tutor-fc-slot-header-divider",
                dayCellClass: "tutor-fc-day-cell",
                dayCellTopClass: "tutor-fc-day-cell-top",
                dayCellTopInnerClass:
                    "tutor-fc-day-cell-top-inner",
                dayCellInnerClass:
                    "tutor-fc-day-cell-inner",
                eventInnerClass: "tutor-fc-event-inner",
                columnEventClass: "tutor-fc-column-event",
                listItemEventClass: "tutor-fc-list-event",
                listItemEventBeforeClass:
                    "tutor-fc-event-dot-hidden",
                moreLinkClass: "tutor-fc-more-link",
                moreLinkInnerClass:
                    "tutor-fc-more-link-inner",
                moreLinkContent: (argument) =>
                    argument.numericText,
                moreLinkClick: "popover",
                views: {
                    timeGrid: {
                        slotHeaderInterval: "02:00:00",
                        slotHeaderFormat: {
                            hour: "2-digit",
                            minute: "2-digit",
                            hour12: false
                        },
                        slotHeaderClass:
                            "tutor-fc-slot-header",
                        slotHeaderInnerClass:
                            "tutor-fc-slot-header-inner",
                        slotLaneClass: "tutor-fc-slot-lane",
                        dayLaneClass: (argument) =>
                            `tutor-fc-day-lane${
                                argument.isToday
                                    ? " is-today"
                                    : ""}`
                    },
                    timeGridSevenDay: {
                        type: "timeGrid",
                        duration: { days: 7 },
                        dateAlignment: "day"
                    },
                    timeGridDay: {
                        slotMinTime: dailyStartTime,
                        scrollTime: dailyStartTime
                    },
                    dayGridMonth: {
                        dayMaxEvents: 5
                    }
                },
                events,
                eventContent: buildEventContent,
                datesSet: ({ view }) => updateRangeLabel(view)
            });

        calendar.render();

        const activateView = () => {
            calendar.changeView(
                fullCalendarViews[selectedView]);

            window.requestAnimationFrame(() => {
                calendar.updateSize();
            });

            if (viewTitle) {
                viewTitle.textContent =
                    viewTitles[selectedView] ||
                    "Availability";
            }

            if (viewLabel) {
                viewLabel.textContent =
                    selectedView.charAt(0).toUpperCase() +
                    selectedView.slice(1);
            }

            viewOptions.forEach((option) => {
                const isSelected =
                    option.dataset.availabilityViewOption ===
                        selectedView;

                option.classList.toggle(
                    "is-active",
                    isSelected);
                option.setAttribute(
                    "aria-current",
                    isSelected ? "true" : "false");
            });

        };

        viewOptions.forEach((option) => {
            option.addEventListener("click", () => {
                selectedView =
                    option.dataset.availabilityViewOption;
                activateView();
            });
        });

        updateRangeLabel(calendar.view);
    };

    setupCountdown();
    setupAvailabilityViews();
    setupBookingPopover();
})();
