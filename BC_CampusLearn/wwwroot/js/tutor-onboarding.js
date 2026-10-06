(() => {
    const modalElement = document.getElementById("tutor-onboarding-modal");
    if (!modalElement || !window.bootstrap) return;

    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const show = () => {
        const other = document.querySelector(".modal.show:not(#tutor-onboarding-modal)");
        if (other) {
            other.addEventListener("hidden.bs.modal", show, { once: true });
            return;
        }
        modal.show();
    };
    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", show, { once: true });
    else show();

    modalElement.addEventListener("shown.bs.modal", () => {
        modalElement.querySelector("textarea")?.focus();
    });
    const form = modalElement.querySelector("[data-tutor-onboarding-form]");
    const button = form.querySelector("button[type='submit']");
    const errors = form.querySelector(".tutor-onboarding-errors");
    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (button.disabled) return;
        const originalLabel = button.textContent;
        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        button.textContent = "Saving your profile…";
        errors.hidden = true;
        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { Accept: "application/json" },
                credentials: "same-origin"
            });
            if (response.redirected) {
                window.location.assign(response.url);
                return;
            }
            const contentType = response.headers.get("content-type") ?? "";
            const result = contentType.includes("application/json") ? await response.json() : null;
            if (response.ok && result?.nextUrl) {
                window.location.assign(result.nextUrl);
                return;
            }
            const messages = result?.errors ?? [
                response.status === 403 ? "Your tutor approval has changed. Refresh the page to continue." :
                response.status === 400 ? "Your session may have expired. Refresh the page and try again." :
                "We couldn’t save your profile. Please try again."
            ];
            errors.replaceChildren(...messages.map(message => {
                const line = document.createElement("p");
                line.textContent = message;
                return line;
            }));
            errors.hidden = false;
            errors.focus();
        } catch {
            errors.textContent = "We couldn’t connect. Your details are still here; please try saving again.";
            errors.hidden = false;
            errors.focus();
        } finally {
            button.disabled = false;
            button.removeAttribute("aria-busy");
            button.textContent = originalLabel;
        }
    });
})();
