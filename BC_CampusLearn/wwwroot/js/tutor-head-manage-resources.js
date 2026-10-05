(() => {
    document.querySelectorAll("[data-nominated-modules-toggle]").forEach((button) => {
        const moduleList = document.getElementById(button.getAttribute("aria-controls"));
        if (!moduleList) return;

        button.addEventListener("click", () => {
            const expanded = button.getAttribute("aria-expanded") !== "true";
            button.setAttribute("aria-expanded", String(expanded));
            button.setAttribute("aria-label", `${expanded ? "Hide" : "Show"} modules for ${button.dataset.tutorName}`);
            moduleList.hidden = !expanded;
        });
    });

    const tutorSelect = document.querySelector("[data-nomination-tutor]");
    const moduleSelect = document.querySelector("[data-nomination-module]");
    if (!tutorSelect || !moduleSelect) return;

    const moduleOptions = Array.from(
        moduleSelect.querySelectorAll("option[data-tutor-id]"));
    const promptOption = moduleSelect.querySelector("option:not([data-tutor-id])");

    const updateModules = () => {
        const tutorId = tutorSelect.value;
        moduleSelect.value = "";
        moduleSelect.disabled = !tutorId;
        if (promptOption) {
            promptOption.textContent = tutorId
                ? "Select a module"
                : "Select a tutor first";
        }

        moduleOptions.forEach((option) => {
            const matchesTutor = option.dataset.tutorId === tutorId;
            option.disabled = !matchesTutor;
            option.hidden = !matchesTutor;
        });
    };

    tutorSelect.addEventListener("change", updateModules);
    updateModules();
})();
