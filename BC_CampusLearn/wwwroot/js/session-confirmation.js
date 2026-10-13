(() => {
    document.querySelectorAll("form[data-session-confirm-form='true']").forEach((form) => {
        const modal = form.closest(".modal");
        const error = form.querySelector("[data-confirm-error]");
        const progress = form.querySelector("[data-confirm-progress]");
        const link = form.querySelector("input[type='url']");
        let submitting = false;

        modal?.addEventListener("show.bs.modal", () => {
            if (error) { error.textContent = ""; error.hidden = true; }
        });
        modal?.addEventListener("hide.bs.modal", (event) => {
            if (submitting) event.preventDefault();
        });

        form.addEventListener("submit", async (event) => {
            event.preventDefault();
            if (submitting || !form.reportValidity()) return;
            const data = new FormData(form);
            const buttons = [...form.querySelectorAll("button")];
            const disabledStates = buttons.map((button) => button.disabled);
            submitting = true;
            form.setAttribute("aria-busy", "true");
            buttons.forEach((button) => { button.disabled = true; });
            if (error) { error.textContent = ""; error.hidden = true; }
            if (progress) {
                progress.textContent = link?.value.trim()
                    ? "Confirming the session…"
                    : "Creating your Teams meeting and confirming the session…";
                progress.hidden = false;
            }
            try {
                const response = await fetch(form.action, {
                    method: "POST", body: data, credentials: "same-origin",
                    headers: { Accept: "application/json" }
                });
                if (!response.ok || !response.headers.get("content-type")?.includes("application/json"))
                    throw new Error("The confirmation could not be verified. Reload the page to check the session before retrying.");
                const result = await response.json();
                if (result.success === true) {
                    window.location.reload();
                    return;
                }
                if (error) {
                    error.textContent = result.message || "The session could not be confirmed. Retry or enter a manual meeting link.";
                    error.hidden = false;
                }
                if (link && !link.value.trim()) { link.readOnly = false; link.focus(); }
            } catch (failure) {
                if (error) {
                    error.textContent = failure.message || "The confirmation could not be verified. Reload the page before retrying.";
                    error.hidden = false;
                }
            } finally {
                submitting = false;
                form.removeAttribute("aria-busy");
                buttons.forEach((button, index) => { button.disabled = disabledStates[index]; });
                if (progress) progress.hidden = true;
            }
        });
    });
})();
