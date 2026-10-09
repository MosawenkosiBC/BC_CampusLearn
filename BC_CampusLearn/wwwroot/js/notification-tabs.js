(() => {
    document.querySelectorAll("[data-notification-tabs]").forEach((tabList) => {
        const tabs = [...tabList.querySelectorAll('[role="tab"]')];
        const activate = (tab) => {
            tabs.forEach((item) => {
                const selected = item === tab;
                item.classList.toggle("selected", selected);
                item.setAttribute("aria-selected", String(selected));
                item.tabIndex = selected ? 0 : -1;
                const panel = document.getElementById(item.getAttribute("aria-controls"));
                if (panel) panel.hidden = !selected;
            });
            const url = new URL(window.location.href);
            const parameter = tabList.dataset.tabParameter;
            url.searchParams.set(parameter, tab.dataset.tabValue);
            if (parameter === "Category") url.searchParams.set("Tab", "notifications");
            url.hash = url.searchParams.get("Tab") === "notifications" ? "admin-notification-settings" : "";
            window.history.replaceState(null, "", url);
        };
        tabs.forEach((tab, index) => {
            tab.addEventListener("click", (event) => {
                if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
                event.preventDefault();
                activate(tab);
            });
            tab.addEventListener("keydown", (event) => {
                let next;
                if (event.key === "ArrowRight") next = (index + 1) % tabs.length;
                else if (event.key === "ArrowLeft") next = (index - 1 + tabs.length) % tabs.length;
                else if (event.key === "Home") next = 0;
                else if (event.key === "End") next = tabs.length - 1;
                else return;
                event.preventDefault();
                activate(tabs[next]);
                tabs[next].focus();
            });
        });
    });
})();
