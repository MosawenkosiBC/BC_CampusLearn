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
        const triggers = Array.from(
            document.querySelectorAll(
                "[data-booking-popover-trigger]"));

        if (!popover || triggers.length === 0) {
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

        triggers.forEach((trigger) => {
            trigger.addEventListener(
                "mouseenter",
                () => openPopover(trigger));
            trigger.addEventListener(
                "mouseleave",
                scheduleClose);
            trigger.addEventListener(
                "focus",
                () => openPopover(trigger));
            trigger.addEventListener(
                "blur",
                scheduleClose);
            trigger.addEventListener("click", () => {
                if (activeTrigger === trigger &&
                    !popover.hidden) {
                    cancelScheduledClose();
                    return;
                }

                openPopover(trigger);
            });
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
        const views = Array.from(
            document.querySelectorAll(
                "[data-availability-view]"));
        const ranges = Array.from(
            document.querySelectorAll(
                "[data-availability-range]"));
        const viewTitle = document.querySelector(
            "[data-availability-title]");
        const viewTitles = {
            weekly: "Weekly Availability",
            monthly: "Monthly Availability",
            daily: "Daily Availability"
        };
        let selectedView = "weekly";

        if (viewOptions.length === 0 ||
            views.length === 0) {
            return;
        }

        document
            .querySelectorAll(".tutor-slot-tab")
            .forEach((slot, index) => {
                slot.style.setProperty(
                    "--slot-delay",
                    `${Math.min(index, 12) * 35}ms`);
            });

        const scrollElementIntoContext = (
            container,
            element) => {
            const containerRect =
                container.getBoundingClientRect();
            const elementRect =
                element.getBoundingClientRect();
            const elementPosition =
                elementRect.top -
                containerRect.top +
                container.scrollTop;
            const contextOffset =
                container.clientHeight * 0.3;

            container.scrollTop = Math.max(
                0,
                elementPosition - contextOffset);
        };

        const positionSelectedView = (view) => {
            const schedule = view.querySelector(
                "[data-current-hour-scroll]");

            if (schedule) {
                const currentHour = new Date().getHours();
                const currentHourRow = schedule.querySelector(
                    `[data-availability-hour="${currentHour}"]`);

                if (currentHourRow) {
                    scrollElementIntoContext(
                        schedule,
                        currentHourRow);
                }

                return;
            }

            const monthScroll = view.querySelector(
                ".tutor-monthly-scroll");
            const today = view.querySelector(
                ".tutor-monthly-day.is-today");

            if (monthScroll && today) {
                scrollElementIntoContext(monthScroll, today);
            }
        };

        const activateView = () => {
            let activeView = null;

            views.forEach((view) => {
                const isActive =
                    view.dataset.availabilityView ===
                        selectedView;
                view.classList.remove("is-entering");

                if (isActive) {
                    view.hidden = false;
                    void view.offsetWidth;
                    view.classList.add("is-entering");
                    activeView = view;
                } else {
                    view.hidden = true;
                }
            });

            ranges.forEach((range) => {
                range.hidden =
                    range.dataset.availabilityRange !==
                        selectedView;
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

            if (activeView) {
                window.requestAnimationFrame(() => {
                    window.requestAnimationFrame(() =>
                        positionSelectedView(activeView));
                });
            }
        };

        viewOptions.forEach((option) => {
            option.addEventListener("click", () => {
                selectedView =
                    option.dataset.availabilityViewOption;
                activateView();
            });
        });

        activateView();

        if (document.readyState !== "complete") {
            window.addEventListener(
                "load",
                activateView,
                { once: true });
        }
    };

    setupCountdown();
    setupBookingPopover();
    setupAvailabilityViews();
})();
