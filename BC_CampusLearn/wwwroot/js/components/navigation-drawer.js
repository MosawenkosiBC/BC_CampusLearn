(() => {
    const drawerComponents = document.querySelectorAll(
        "[data-navigation-drawer]");

    drawerComponents.forEach((component) => {
        const openButton = component.querySelector("[data-drawer-open]");
        const closeButton = component.querySelector("[data-drawer-close]");
        const backdrop = component.querySelector("[data-drawer-backdrop]");
        const panel = component.querySelector("[data-drawer-panel]");

        if (!openButton || !closeButton || !backdrop || !panel) {
            return;
        }

        let previouslyFocusedElement = null;

        const getFocusableElements = () =>
            Array.from(panel.querySelectorAll(
                "a[href], button:not([disabled]), [tabindex]:not([tabindex='-1'])"));

        const openDrawer = () => {
            previouslyFocusedElement = document.activeElement;
            component.classList.add("is-open");
            document.body.classList.add("navigation-drawer-open");
            openButton.setAttribute("aria-expanded", "true");
            panel.setAttribute("aria-hidden", "false");
            panel.inert = false;
            closeButton.focus();
        };

        const closeDrawer = () => {
            component.classList.remove("is-open");
            document.body.classList.remove("navigation-drawer-open");
            openButton.setAttribute("aria-expanded", "false");
            panel.setAttribute("aria-hidden", "true");
            panel.inert = true;

            if (previouslyFocusedElement instanceof HTMLElement) {
                previouslyFocusedElement.focus();
            } else {
                openButton.focus();
            }
        };

        openButton.addEventListener("click", openDrawer);
        closeButton.addEventListener("click", closeDrawer);
        backdrop.addEventListener("click", closeDrawer);

        document.addEventListener("keydown", (event) => {
            if (!component.classList.contains("is-open")) {
                return;
            }

            if (event.key === "Escape") {
                closeDrawer();
                return;
            }

            if (event.key !== "Tab") {
                return;
            }

            const focusableElements = getFocusableElements();
            const firstElement = focusableElements[0];
            const lastElement = focusableElements.at(-1);

            if (!firstElement || !lastElement) {
                event.preventDefault();
                panel.focus();
                return;
            }

            if (event.shiftKey && document.activeElement === firstElement) {
                event.preventDefault();
                lastElement.focus();
            } else if (!event.shiftKey && document.activeElement === lastElement) {
                event.preventDefault();
                firstElement.focus();
            }
        });
    });
})();
