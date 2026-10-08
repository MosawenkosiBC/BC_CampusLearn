(() => {
    const modalElement = document.getElementById("tutor-onboarding-modal");
    if (modalElement && window.bootstrap) {
        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
        const storageKey = modalElement.dataset.onboardingStorageKey;
        const loginId = modalElement.dataset.onboardingLoginId;
        const isDismissed = () => {
            try { return localStorage.getItem(storageKey) === loginId; }
            catch { return false; }
        };
        const show = () => {
            if (isDismissed()) return;
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

        modalElement.addEventListener("hidden.bs.modal", () => {
            try { localStorage.setItem(storageKey, loginId); }
            catch { /* The close button still works when browser storage is unavailable. */ }
        });
        modalElement.addEventListener("shown.bs.modal", () => {
            modalElement.querySelector("textarea")?.focus();
        });
    }

    document.querySelectorAll("[data-tutor-onboarding-form]").forEach(form => {
        const button = form.querySelector("button[type='submit']");
        const errors = form.querySelector(".tutor-onboarding-errors");
        const biography = form.querySelector("[name='Input.Biography']");
        const bioError = form.querySelector("[data-onboarding-bio-error]");
        biography.addEventListener("input", () => {
            bioError.hidden = true;
            bioError.textContent = "";
            biography.removeAttribute("aria-invalid");
        });
        form.addEventListener("submit", async event => {
            event.preventDefault();
            if (button.disabled) return;
            errors.hidden = true;
            const bio = biography.value.trim();
            const bioMessage = !bio ? "Write a short bio to introduce yourself to students."
                : bio.length < 30 || bio.length > 500 ? "Your bio must be between 30 and 500 characters." : null;
            if (bioMessage) {
                bioError.textContent = bioMessage;
                bioError.hidden = false;
                biography.setAttribute("aria-invalid", "true");
                biography.focus();
                return;
            }
            bioError.hidden = true;
            biography.removeAttribute("aria-invalid");
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
    });
})();
