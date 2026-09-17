(() => {
    const modal = document.querySelector("[data-notification-modal]");
    if (!(modal instanceof HTMLDialogElement)) {
        return;
    }

    modal.showModal();

    const closeModal = () => modal.close();
    modal.querySelectorAll("[data-notification-modal-close]")
        .forEach(button => button.addEventListener("click", closeModal));

    modal.addEventListener("close", () => {
        const url = new URL(window.location.href);
        url.searchParams.delete("notificationId");
        window.history.replaceState(
            {},
            "",
            `${url.pathname}${url.search}${url.hash}`);
    });

    modal.addEventListener("click", event => {
        if (event.target === modal) {
            closeModal();
        }
    });
})();
