(() => {
    document.querySelectorAll("form[data-session-confirm-form='true']").forEach((form) => {
        const modal = form.closest(".modal");
        const error = form.querySelector("[data-confirm-error]");
        const progress = form.querySelector("[data-confirm-progress]");
        const link = form.querySelector("input[type='url']");
        const generator = form.querySelector("[data-generate-teams-link]");
        let submitting = false;

        const updateGenerator = () => {
            if (!generator) return;
            const hidden = Boolean(link?.value.trim());
            generator.hidden = hidden;
            generator.classList.toggle("d-none", hidden);
        };
        link?.addEventListener("input", updateGenerator);
        updateGenerator();

        modal?.addEventListener("show.bs.modal", () => {
            if (error) { error.textContent = ""; error.hidden = true; }
            updateGenerator();
        });
        modal?.addEventListener("hide.bs.modal", (event) => {
            if (submitting) event.preventDefault();
        });

        const send = async (generating) => {
            if (submitting || (!generating && !form.reportValidity())) return;
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
                    : generating ? "Generating your Teams link…" : "Creating your Teams meeting and confirming the session…";
                progress.hidden = false;
            }
            try {
                const response = await fetch(generating ? generator.dataset.generateUrl : form.action, {
                    method: "POST", body: data, credentials: "same-origin",
                    headers: { Accept: "application/json" }
                });
                if (!response.ok || !response.headers.get("content-type")?.includes("application/json"))
                    throw new Error(generating
                        ? "The meeting creation result could not be verified. Reload the page before generating another link."
                        : "The confirmation could not be verified. Reload the page to check the session before retrying.");
                const result = await response.json();
                if (result.success === true) {
                    if (generating) {
                        if (!link || typeof result.link !== "string" || !result.link.trim())
                            throw new Error("The meeting link could not be retrieved. Please retry.");
                        link.value = result.link;
                        link.readOnly = true;
                        updateGenerator();
                        // Keep the link when the sessions-list modal is closed and
                        // reopened, without generating another invitation.
                        document.querySelectorAll("[data-tutor-confirm-session]").forEach((button) => {
                            if (button.dataset.bookingId === data.get("bookingId")) {
                                button.dataset.meetingLink = result.link;
                                button.dataset.generatedMeeting = "true";
                            }
                        });
                        return;
                    }
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
        };
        form.addEventListener("submit", (event) => {
            event.preventDefault();
            return send(false);
        });
        generator?.addEventListener("click", () => {
            if (!generator.hidden) return send(true);
        });
    });
})();
