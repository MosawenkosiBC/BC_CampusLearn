(() => {
    const successModal = document.getElementById("teams-meeting-success-modal");
    if (successModal) bootstrap.Modal.getOrCreateInstance(successModal).show();

    document.querySelectorAll("form[data-session-confirm-form='true']").forEach((form) => {
        const modal = form.closest(".modal");
        const error = form.querySelector("[data-confirm-error]");
        const progress = form.querySelector("[data-confirm-progress]");
        const link = form.querySelector("input[type='url']");
        const generator = form.querySelector("[data-generate-teams-link]");
        const manualSection = form.querySelector("[data-manual-link-section]");
        const manualToggle = form.querySelector("[data-toggle-manual-link]");
        const success = form.querySelector("[data-confirm-success]");
        let submitting = false;
        let completed = false;

        const setManualVisible = (visible) => {
            if (manualSection) manualSection.hidden = !visible;
            manualToggle?.setAttribute("aria-expanded", String(visible));
            if (manualToggle) {
                manualToggle.textContent = visible ? "−" : "+";
                manualToggle.setAttribute("aria-label", visible ? "Hide manual meeting link" : "Add a manual meeting link");
            }
        };
        manualToggle?.addEventListener("click", () => {
            setManualVisible(manualSection?.hidden);
            if (!manualSection?.hidden) link?.focus();
        });

        modal?.addEventListener("show.bs.modal", () => {
            if (error) { error.textContent = ""; error.hidden = true; }
            setManualVisible(!manualToggle);
        });
        modal?.addEventListener("hide.bs.modal", (event) => {
            if (submitting && !completed) event.preventDefault();
        });
        modal?.addEventListener("hidden.bs.modal", () => {
            if (completed) window.location.reload();
        });
        success?.querySelector("button")?.addEventListener("click", () => window.location.reload());

        const send = async (generating) => {
            if (submitting || completed) return;
            if (!generating) {
                setManualVisible(true);
                if (link) link.value = link.value.trim();
                const message = !link?.value
                    ? "Enter a meeting link to confirm the session."
                    : !form.checkValidity() ? "Enter a valid HTTP or HTTPS meeting link." : "";
                if (message) {
                    if (error) { error.textContent = message; error.hidden = false; }
                    link?.focus();
                    return;
                }
            }
            const data = new FormData(form);
            const buttons = [...form.querySelectorAll("button")];
            const disabledStates = buttons.map((button) => button.disabled);
            const wasReadOnly = link?.readOnly ?? false;
            let confirmed = false;
            submitting = true;
            if (link) link.readOnly = true;
            form.setAttribute("aria-busy", "true");
            buttons.forEach((button) => { button.disabled = true; });
            if (error) { error.textContent = ""; error.hidden = true; }
            if (progress) {
                progress.textContent = generating
                    ? "Creating your Teams meeting and confirming the session…"
                    : "Confirming the session…";
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
                        if (manualToggle) manualToggle.hidden = true;
                        setManualVisible(false);
                    }
                    confirmed = true;
                    completed = true;
                    form.removeAttribute("aria-busy");
                    if (success) {
                        success.querySelector("button").disabled = false;
                        success.querySelector("p").textContent = generating
                            ? "Your Teams meeting link has been generated, the invitation sent to the student, and the session confirmed."
                            : "Your meeting link has been saved and the session confirmed.";
                        const body = success.parentElement;
                        Array.from(body.children).forEach(child => { child.hidden = child !== success; });
                        const footer = form.querySelector(".modal-footer");
                        if (footer) footer.hidden = true;
                        const title = form.querySelector(".modal-header h2");
                        if (title) title.textContent = "Session confirmed";
                        form.querySelectorAll("[data-bs-dismiss='modal']").forEach(button => { button.disabled = false; });
                    } else {
                        window.location.reload();
                    }
                    return;
                }
                if (error) {
                    error.textContent = result.message || "The session could not be confirmed. Retry or enter a manual meeting link.";
                    error.hidden = false;
                }
                if (!generating && link) link.focus();
            } catch (failure) {
                if (error) {
                    error.textContent = failure.message || "The confirmation could not be verified. Reload the page before retrying.";
                    error.hidden = false;
                }
            } finally {
                if (!confirmed) {
                    submitting = false;
                    if (link) link.readOnly = wasReadOnly;
                    form.removeAttribute("aria-busy");
                    buttons.forEach((button, index) => { button.disabled = disabledStates[index]; });
                    if (progress) progress.hidden = true;
                }
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
