(() => {
    const grid = document.getElementById("tutor-results-grid");
    if (grid) {
        const updateProfileLinks = () => {
            grid.querySelectorAll(".tutor-card-link").forEach(link => {
                const returnUrl = new URL(window.location.href);
                returnUrl.hash = link.closest("[data-tutor-search-card]").id;
                const profileUrl = new URL(link.href);
                profileUrl.searchParams.set("returnUrl",
                    `${returnUrl.pathname}${returnUrl.search}${returnUrl.hash}`);
                link.href = profileUrl.href;
            });
        };

        // Capture the position before following a profile, including keyboard navigation.
        grid.addEventListener("click", event => {
            if (!event.target.closest(".tutor-card-link")) return;
            window.history.replaceState({
                ...window.history.state,
                tutorDirectoryScroll: { x: window.scrollX, y: window.scrollY }
            }, "");
            updateProfileLinks();
        });
        grid.addEventListener("auxclick", updateProfileLinks);
        grid.addEventListener("contextmenu", updateProfileLinks);
        updateProfileLinks();

        window.addEventListener("pageshow", event => {
            // A cached page already has its DOM and native scroll position intact.
            if (event.persisted) return;
            const position = window.history.state?.tutorDirectoryScroll;
            if (Number.isFinite(position?.x) && Number.isFinite(position?.y)) {
                window.scrollTo({ left: position.x, top: position.y, behavior: "instant" });
            } else if (window.location.hash.startsWith("#tutor-")) {
                document.getElementById(window.location.hash.slice(1))?.scrollIntoView();
            }
        });
    }

    const backLink = document.querySelector("[data-tutor-directory-return]");
    const returnUrl = new URL(window.location.href).searchParams.get("returnUrl");
    if (backLink && returnUrl) {
        try {
            const destination = new URL(returnUrl, window.location.origin);
            const directory = new URL(backLink.href);
            if (destination.origin === directory.origin &&
                destination.pathname === directory.pathname) {
                backLink.href = destination.href;
            }
        } catch {
            // Keep the directory link when the supplied return URL is malformed.
        }
    }
})();
