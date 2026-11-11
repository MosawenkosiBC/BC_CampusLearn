(() => {
    const details = document.getElementById("survey-question-details");
    if (!details || typeof details.showModal !== "function") return;

    details.close();
    details.showModal();
    details.addEventListener("cancel", event => {
        event.preventDefault();
        window.location.assign(details.querySelector("[data-close-details]").href);
    });
})();
