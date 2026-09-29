(() => {
    const page = document.querySelector("[data-settings-page]");
    if (!page) return;

    const links = [...page.querySelectorAll(".settings-navigation a")];
    const activate = hash => {
        links.forEach(link => link.classList.toggle(
            "is-active",
            link.getAttribute("href") === hash));
    };

    links.forEach(link => link.addEventListener("click", () => {
        activate(link.getAttribute("href"));
    }));

    if (window.location.hash) activate(window.location.hash);

    page.querySelectorAll("form[data-confirm]").forEach(form => {
        form.addEventListener("submit", event => {
            if (!window.confirm(form.dataset.confirm)) event.preventDefault();
        });
    });
})();
