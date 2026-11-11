(() => {
    const modal = document.getElementById("dashboard-survey-modal");
    if (!modal || !window.bootstrap) return;

    const showSurvey = () => window.bootstrap.Modal.getOrCreateInstance(modal).show();
    const applicationModal = document.querySelector('#senior-tutor-application-modal[data-open-on-load="true"]');
    if (applicationModal) {
        applicationModal.addEventListener("hidden.bs.modal", showSurvey, { once: true });
    } else {
        showSurvey();
    }
})();
