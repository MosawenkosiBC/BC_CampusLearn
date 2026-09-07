(() => {
    const modalElement = document.getElementById(
        "bookings-cancel-session-modal");
    const form = modalElement?.querySelector("[data-bookings-cancel-form]");
    const bookingIdInput = form?.querySelector("[data-bookings-cancel-id]");
    const reasonInput = form?.querySelector("[data-bookings-cancel-reason]");
    const description = modalElement?.querySelector(
        "[data-bookings-cancel-description]");

    document.querySelectorAll("[data-booking-cancel]").forEach((button) => {
        button.addEventListener("click", () => {
            if (!form || !bookingIdInput || !reasonInput) {
                return;
            }

            bookingIdInput.value = button.dataset.bookingId || "";
            reasonInput.value = "";

            if (button.dataset.reasonRequired !== "true") {
                reasonInput.required = false;
                form.requestSubmit();
                return;
            }

            reasonInput.required = true;
            const tutorName = button.dataset.tutorName || "the tutor";
            if (description) {
                description.textContent =
                    `Tell ${tutorName} why this confirmed session is being cancelled.`;
            }

            if (modalElement && window.bootstrap) {
                bootstrap.Modal.getOrCreateInstance(modalElement).show();
            }
        });
    });
})();
