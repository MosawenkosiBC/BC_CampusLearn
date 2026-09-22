// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
    const modalElement = document.querySelector(
        "[data-join-meeting-unavailable-modal]");
    const messageElement = modalElement?.querySelector(
        "[data-join-meeting-unavailable-message]");

    if (!modalElement || !messageElement) {
        return;
    }

    if (modalElement.parentElement !== document.body) {
        document.body.append(modalElement);
    }

    document.addEventListener("click", (event) => {
        const trigger = event.target.closest("[data-join-meeting-trigger]");
        if (!trigger || trigger.getAttribute("aria-disabled") !== "true") {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        messageElement.textContent =
            trigger.dataset.meetingLinkAvailable === "false"
                ? "The meeting link is not available yet. Please check again closer to the scheduled session time."
                : "You can join the meeting five minutes before the scheduled start time.";

        if (window.bootstrap) {
            bootstrap.Modal.getOrCreateInstance(modalElement).show();
        }
    });
})();
