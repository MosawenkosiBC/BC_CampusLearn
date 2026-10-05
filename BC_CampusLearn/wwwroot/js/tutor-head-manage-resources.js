(() => {
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
