(() => {
    const modal = document.getElementById("compensation-filter-modal");
    if (!modal) return;
    const period = modal.querySelector("[data-compensation-period]");
    const dates = modal.querySelector("[data-compensation-custom-dates]");
    const updateDates = () => {
        const custom = period.value === "custom";
        dates.hidden = !custom;
        dates.querySelectorAll("input").forEach(input => {
            input.disabled = !custom;
            input.required = custom;
        });
    };
    period.addEventListener("change", updateDates);
    updateDates();
    if (modal.dataset.openOnLoad === "true" && window.bootstrap)
        bootstrap.Modal.getOrCreateInstance(modal).show();
})();
