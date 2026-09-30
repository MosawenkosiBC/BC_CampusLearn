(() => {
    const modalElement = document.getElementById("gemini-key-modal");
    if (!modalElement) return;

    const input = modalElement.querySelector('input[type="password"], input[data-api-key]');
    const toggle = modalElement.querySelector("[data-toggle-api-key]");
    toggle?.addEventListener("click", () => {
        if (!input) return;
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        toggle.textContent = show ? "Hide" : "Show";
        toggle.setAttribute("aria-label", `${show ? "Hide" : "Show"} API key`);
    });

    if (modalElement.dataset.open === "true" && window.bootstrap?.Modal) {
        window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }
})();
