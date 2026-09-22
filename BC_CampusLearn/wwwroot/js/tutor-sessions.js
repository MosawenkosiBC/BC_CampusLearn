(() => {
    const modalElement = document.getElementById(
        "tutor-confirm-session-modal");
    const form = modalElement?.querySelector("[data-tutor-confirm-form]");
    const bookingIdInput = form?.querySelector(
        "[data-tutor-confirm-booking-id]");
    const meetingLinkInput = form?.querySelector("input[name='meetingLink']");
    const description = modalElement?.querySelector(
        "[data-tutor-confirm-description]");

    document.querySelectorAll("[data-tutor-confirm-session]")
        .forEach((button) => button.addEventListener("click", () => {
            if (!modalElement || !bookingIdInput || !meetingLinkInput ||
                !window.bootstrap) {
                return;
            }

            bookingIdInput.value = button.dataset.bookingId || "";
            meetingLinkInput.value = "";
            const studentName = button.dataset.studentName || "the student";
            if (description) {
                description.textContent =
                    `Add the meeting link ${studentName} will use to join.`;
            }

            bootstrap.Modal.getOrCreateInstance(modalElement).show();
            modalElement.addEventListener(
                "shown.bs.modal",
                () => meetingLinkInput.focus(),
                { once: true });
        }));

    const cancelModalElement = document.getElementById(
        "tutor-cancel-session-modal");
    const cancelForm = cancelModalElement?.querySelector(
        "[data-tutor-cancel-form]");
    const cancelBookingIdInput = cancelForm?.querySelector(
        "[data-tutor-cancel-booking-id]");
    const cancellationReasonInput = cancelForm?.querySelector(
        "[data-tutor-cancel-reason]");
    const cancellationReasonError = cancelForm?.querySelector(
        "[data-tutor-cancel-reason-error]");
    const reopenAvailabilityInput = cancelForm?.querySelector(
        "[data-tutor-cancel-reopen]");
    const cancelDescription = cancelModalElement?.querySelector(
        "[data-tutor-cancel-description]");

    const setCancellationReasonError = (message) => {
        if (!cancellationReasonInput || !cancellationReasonError) {
            return;
        }

        const hasError = Boolean(message);
        cancellationReasonInput.setAttribute(
            "aria-invalid",
            hasError.toString());
        cancellationReasonError.textContent = message || "";
        cancellationReasonError.hidden = !hasError;
    };

    const validateCancellationReason = () => {
        if (!cancellationReasonInput) {
            return false;
        }

        const reasonLength = cancellationReasonInput.value.trim().length;
        let message = "";

        if (reasonLength < 5) {
            message = "A reason is required to cancel a confirmed session.";
        } else if (reasonLength > 1000) {
            message = "Please enter no more than 1000 characters.";
        }

        setCancellationReasonError(message);
        return !message;
    };

    cancelForm?.addEventListener("submit", (event) => {
        if (validateCancellationReason()) {
            return;
        }

        event.preventDefault();
        cancellationReasonInput?.focus();
    });

    cancellationReasonInput?.addEventListener("input", () => {
        if (cancellationReasonInput.getAttribute("aria-invalid") === "true") {
            validateCancellationReason();
        }
    });

    document.querySelectorAll("[data-tutor-cancel-session]")
        .forEach((button) => button.addEventListener("click", () => {
            if (!cancelModalElement || !cancelBookingIdInput ||
                !cancellationReasonInput || !window.bootstrap) {
                return;
            }

            cancelBookingIdInput.value = button.dataset.bookingId || "";
            cancellationReasonInput.value = "";
            if (reopenAvailabilityInput) {
                reopenAvailabilityInput.checked = true;
            }
            setCancellationReasonError("");
            const studentName = button.dataset.studentName || "the student";
            if (cancelDescription) {
                cancelDescription.textContent =
                    `Tell ${studentName} why this confirmed session is being cancelled.`;
            }

            bootstrap.Modal.getOrCreateInstance(cancelModalElement).show();
            cancelModalElement.addEventListener(
                "shown.bs.modal",
                () => cancellationReasonInput.focus(),
                { once: true });
        }));

    const declineModalElement = document.getElementById(
        "tutor-decline-booking-modal");
    const declineForm = declineModalElement?.querySelector(
        "[data-tutor-decline-form]");
    const declineBookingIdInput = declineForm?.querySelector(
        "[data-tutor-decline-booking-id]");
    const customReasonContainer = declineModalElement?.querySelector(
        "[data-tutor-decline-custom]");
    const customReasonInput = declineModalElement?.querySelector(
        "[data-tutor-decline-custom-reason]");

    const updateCustomReason = () => {
        const selectedReason = declineForm?.querySelector(
            "input[name='declineReasonOption']:checked");
        const showCustomReason = selectedReason?.value === "other";

        if (customReasonContainer) {
            customReasonContainer.hidden = !showCustomReason;
        }

        if (customReasonInput) {
            customReasonInput.required = showCustomReason;
            if (!showCustomReason) {
                customReasonInput.value = "";
            }
        }
    };

    declineForm?.querySelectorAll("input[name='declineReasonOption']")
        .forEach((input) => input.addEventListener(
            "change",
            updateCustomReason));

    document.querySelectorAll("[data-tutor-decline-booking]")
        .forEach((button) => button.addEventListener("click", () => {
            if (!declineModalElement || !declineForm ||
                !declineBookingIdInput || !window.bootstrap) {
                return;
            }

            declineForm.reset();
            declineBookingIdInput.value = button.dataset.bookingId || "";
            updateCustomReason();

            bootstrap.Modal.getOrCreateInstance(
                declineModalElement).show();
        }));

    [modalElement, declineModalElement, cancelModalElement]
        .forEach((element) => {
            if (element && element.parentElement !== document.body) {
                document.body.append(element);
            }
        });
})();
