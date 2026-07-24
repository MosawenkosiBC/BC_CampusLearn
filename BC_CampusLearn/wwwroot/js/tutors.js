(() => {
    const panel = document.querySelector("[data-filter-panel]");
    const openButton = document.querySelector("[data-filter-open]");
    const closeButtons = document.querySelectorAll("[data-filter-close]");

    if (!panel || !openButton || closeButtons.length === 0) {
        return;
    }

    const mobileQuery = window.matchMedia("(max-width: 767.98px)");
    let lastFocusedElement = null;

    const setAccessibilityState = () => {
        if (mobileQuery.matches && !panel.classList.contains("is-open")) {
            panel.setAttribute("aria-hidden", "true");
            panel.removeAttribute("role");
            panel.removeAttribute("aria-modal");
            panel.inert = true;
        } else {
            panel.removeAttribute("aria-hidden");
            panel.inert = false;
        }
    };

    const openFilters = () => {
        if (!mobileQuery.matches) {
            return;
        }

        lastFocusedElement = document.activeElement;
        panel.classList.add("is-open");
        document.body.classList.add("tutor-filter-open");
        openButton.setAttribute("aria-expanded", "true");
        panel.setAttribute("role", "dialog");
        panel.setAttribute("aria-modal", "true");
        panel.removeAttribute("aria-hidden");
        panel.inert = false;
        panel.querySelector("[data-filter-close]")?.focus();
    };

    const closeFilters = () => {
        panel.classList.remove("is-open");
        document.body.classList.remove("tutor-filter-open");
        openButton.setAttribute("aria-expanded", "false");
        setAccessibilityState();

        if (lastFocusedElement instanceof HTMLElement) {
            lastFocusedElement.focus();
        }
    };

    openButton.addEventListener("click", openFilters);
    closeButtons.forEach(button =>
        button.addEventListener("click", closeFilters));

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && panel.classList.contains("is-open")) {
            closeFilters();
        }
    });

    mobileQuery.addEventListener("change", () => {
        if (!mobileQuery.matches) {
            closeFilters();
        } else {
            setAccessibilityState();
        }
    });

    setAccessibilityState();
})();
