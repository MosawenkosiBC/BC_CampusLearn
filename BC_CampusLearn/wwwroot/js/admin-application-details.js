(() => {
    const reviewForm = document.querySelector("[data-application-review-form]");
    const openButton = document.querySelector("[data-reject-open]");
    const modal = document.querySelector("[data-reject-modal]");
    const closeButton = modal?.querySelector("[data-reject-close]");
    const confirmButton = modal?.querySelector("[data-reject-confirm]");
    const reviewReason = reviewForm?.querySelector("[name='ReviewReason']");
    const shortlistButton = reviewForm?.querySelector(
        "[data-shortlist-submit]");
    const reasonError = reviewForm?.querySelector(
        "[data-review-reason-error]");

    if (!reviewForm || !openButton || !modal || !reviewReason) {
        return;
    }

    openButton.addEventListener("click", () => {
        modal.showModal();
        confirmButton?.focus();
    });

    reviewForm.addEventListener("submit", event => {
        if (event.submitter !== shortlistButton || reviewReason.value.trim()) {
            return;
        }

        event.preventDefault();
        reviewReason.setAttribute("aria-invalid", "true");
        if (reasonError) {
            reasonError.textContent =
                "Enter a reason before approving and shortlisting this candidate.";
        }
        reviewReason.focus();
    });

    reviewReason.addEventListener("input", () => {
        if (!reviewReason.value.trim()) {
            return;
        }
        reviewReason.removeAttribute("aria-invalid");
        if (reasonError) {
            reasonError.textContent = "";
        }
    });

    closeButton?.addEventListener("click", () => modal.close());

    modal.addEventListener("click", event => {
        if (event.target === modal) {
            modal.close();
        }
    });
})();
