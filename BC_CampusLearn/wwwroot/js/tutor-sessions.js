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
    const cancelDescription = cancelModalElement?.querySelector(
        "[data-tutor-cancel-description]");

    document.querySelectorAll("[data-tutor-cancel-session]")
        .forEach((button) => button.addEventListener("click", () => {
            if (!cancelModalElement || !cancelBookingIdInput ||
                !cancellationReasonInput || !window.bootstrap) {
                return;
            }

            cancelBookingIdInput.value = button.dataset.bookingId || "";
            cancellationReasonInput.value = "";
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
