document.addEventListener("DOMContentLoaded", () => {
    const informationAccordionTriggers = Array.from(
        document.querySelectorAll("[data-senior-information-accordion]"));
    const mobileInformationAccordion = window.matchMedia(
        "(max-width: 767.98px)");

    const syncInformationAccordions = () => {
        informationAccordionTriggers.forEach((trigger) => {
            trigger.setAttribute(
                "aria-expanded",
                mobileInformationAccordion.matches ? "false" : "true");
        });
    };

    informationAccordionTriggers.forEach((trigger) => {
        trigger.addEventListener("click", () => {
            if (!mobileInformationAccordion.matches) {
                return;
            }

            const shouldOpen = trigger.getAttribute("aria-expanded") !== "true";
            informationAccordionTriggers.forEach((item) => {
                item.setAttribute("aria-expanded", "false");
            });
            trigger.setAttribute("aria-expanded", String(shouldOpen));
        });
    });

    syncInformationAccordions();
    mobileInformationAccordion.addEventListener(
        "change",
        syncInformationAccordions);

    const form = document.querySelector("[data-senior-tutor-application-form]");
    if (!form) {
        return;
    }

    const average = form.querySelector("[data-senior-average]");
    const descriptions = Array.from(
        form.querySelectorAll("[data-senior-description]"));
    const reason = form.querySelector("[data-senior-reason]");
    const reasonCounter = form.querySelector("[data-senior-reason-counter]");
    const averageError = form.querySelector("[data-senior-average-error]");
    const descriptionError = form.querySelector("[data-senior-description-error]");
    const reasonError = form.querySelector("[data-senior-reason-error]");

    const setError = (field, error, message) => {
        if (error) {
            error.textContent = message;
        }
        field?.setAttribute("aria-invalid", String(Boolean(message)));
    };

    const validateAverage = () => {
        const value = average?.value.trim() ?? "";
        const parsedValue = Number(value);
        const message = value.length === 0
            ? "Enter your academic average."
            : !Number.isFinite(parsedValue) || parsedValue < 72 || parsedValue > 100
                ? "Enter an academic average from 72 to 100."
                : "";
        setError(average, averageError, message);
        return !message;
    };

    const validateDescription = () => {
        const selected = descriptions.find((option) => option.checked);
        const message = selected
            ? ""
            : "Choose the option that describes you best.";
        if (descriptionError) {
            descriptionError.textContent = message;
        }
        descriptions.forEach((option) => option.setAttribute(
            "aria-invalid",
            String(Boolean(message))));
        return !message;
    };

    const validateReason = () => {
        const value = reason?.value.trim() ?? "";
        const message = value.length === 0
            ? "Explain why you are suitable for the senior tutor role."
            : value.length > 2000
                ? "Keep your answer to 2,000 characters or fewer."
                : "";
        setError(reason, reasonError, message);
        return !message;
    };

    const updateReasonCounter = () => {
        if (!reason || !reasonCounter) {
            return;
        }

        const maximumLength = Number(reason.dataset.maxLength);
        if (reason.value.length > maximumLength) {
            reason.value = reason.value.slice(0, maximumLength);
        }

        const remaining = Math.max(maximumLength - reason.value.length, 0);
        reasonCounter.textContent =
            `${remaining.toLocaleString()} characters remaining`;
    };

    average?.addEventListener("input", validateAverage);
    descriptions.forEach((option) => option.addEventListener(
        "change",
        validateDescription));
    reason?.addEventListener("input", () => {
        updateReasonCounter();
        validateReason();
    });

    updateReasonCounter();

    form.addEventListener("submit", (event) => {
        const isAverageValid = validateAverage();
        const isDescriptionValid = validateDescription();
        const isReasonValid = validateReason();

        if (isAverageValid && isDescriptionValid && isReasonValid) {
            return;
        }

        event.preventDefault();
        const firstInvalidField = !isAverageValid
            ? average
            : !isDescriptionValid
                ? descriptions[0]
                : reason;
        firstInvalidField?.focus();
    });
});
