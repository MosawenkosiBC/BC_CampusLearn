(() => {
    const modalElement = document.getElementById(
        "bookings-cancel-session-modal");
    if (modalElement?.parentElement !== document.body) {
        document.body.append(modalElement);
    }

    const form = modalElement?.querySelector("[data-bookings-cancel-form]");
    const bookingIdInput = form?.querySelector("[data-bookings-cancel-id]");
    const reasonInput = form?.querySelector("[data-bookings-cancel-reason]");
    const description = modalElement?.querySelector(
        "[data-bookings-cancel-description]");
    const reasonGroup = modalElement?.querySelector(
        "[data-bookings-cancel-reason-group]");

    document.querySelectorAll("[data-booking-cancel]").forEach((button) => {
        button.addEventListener("click", () => {
            if (!form || !bookingIdInput || !reasonInput || !modalElement) {
                return;
            }

            bookingIdInput.value = button.dataset.bookingId || "";
            reasonInput.value = "";

            const tutorName = button.dataset.tutorName || "the tutor";
            const reasonRequired = button.dataset.reasonRequired === "true";
            reasonInput.required = reasonRequired;
            if (reasonGroup) {
                reasonGroup.hidden = !reasonRequired;
            }

            if (description && reasonRequired) {
                description.textContent =
                    `Tell ${tutorName} why this confirmed session is being cancelled. This action cannot be undone.`;
            } else if (description) {
                description.textContent =
                    `Cancel this pending session with ${tutorName}? This action cannot be undone.`;
            }

            if (window.bootstrap) {
                bootstrap.Modal.getOrCreateInstance(modalElement).show();
            }
        });
    });
})();
