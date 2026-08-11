(() => {
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
