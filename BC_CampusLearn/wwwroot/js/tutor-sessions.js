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
})();
