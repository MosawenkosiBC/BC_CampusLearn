document.addEventListener("DOMContentLoaded", () => {
    const decisionForm = document.getElementById(
        "senior-application-decision-form");
    const reviewNote = decisionForm?.querySelector("#ReviewNote");
    const reviewNoteValidation = decisionForm?.querySelector(
        "[data-review-note-validation]");

    const clearReviewNoteError = () => {
        if (!reviewNote || !reviewNoteValidation) return;
        reviewNote.removeAttribute("aria-invalid");
        reviewNoteValidation.textContent = "";
        reviewNoteValidation.classList.remove("field-validation-error");
        reviewNoteValidation.classList.add("field-validation-valid");
    };

    const validateRejectionNote = (focus = true) => {
        if (!reviewNote || !reviewNoteValidation) return true;
        if (reviewNote.value.trim()) {
            clearReviewNoteError();
            return true;
        }

        reviewNote.setAttribute("aria-invalid", "true");
        reviewNoteValidation.textContent =
            "Enter a decision note before rejecting this application.";
        reviewNoteValidation.classList.remove("field-validation-valid");
        reviewNoteValidation.classList.add("field-validation-error");
        if (focus) reviewNote.focus();
        return false;
    };

    reviewNote?.addEventListener("input", () => {
        if (reviewNote.value.trim()) clearReviewNoteError();
    });

    document.querySelector("[data-senior-reject-trigger]")
        ?.addEventListener("click", event => {
            event.preventDefault();
            if (!validateRejectionNote()) return;

            const modalElement = document.getElementById(
                "reject-senior-application-modal");
            if (modalElement && window.bootstrap) {
                window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
            }
        });

    decisionForm?.addEventListener("submit", event => {
        const isRejection = event.submitter?.matches(
            "[data-senior-reject-confirm]");
        if (!isRejection || validateRejectionNote(false)) return;

        event.preventDefault();
        const modalElement = document.getElementById(
            "reject-senior-application-modal");
        if (modalElement && window.bootstrap) {
            modalElement.addEventListener(
                "hidden.bs.modal",
                () => reviewNote?.focus(),
                { once: true });
            window.bootstrap.Modal.getOrCreateInstance(modalElement).hide();
        } else {
            reviewNote?.focus();
        }
    });

    if (!window.Chart) {
        return;
    }

    const readChartData = (canvas) => ({
        labels: JSON.parse(canvas.dataset.labels || "[]"),
        values: JSON.parse(canvas.dataset.values || "[]")
    });

    const gridColor = "rgba(98, 107, 120, 0.14)";
    const textColor = "#4f565f";
    const baseScales = {
        x: {
            beginAtZero: true,
            grid: { color: gridColor },
            ticks: { color: textColor, precision: 0 }
        },
        y: {
            beginAtZero: true,
            grid: { display: false },
            ticks: { color: textColor, precision: 0 }
        }
    };

    const createBarChart = (selector, color, horizontal = false) => {
        const canvas = document.querySelector(selector);
        if (!canvas) {
            return;
        }

        const data = readChartData(canvas);
        new window.Chart(canvas, {
            type: "bar",
            data: {
                labels: data.labels,
                datasets: [{
                    data: data.values,
                    backgroundColor: color,
                    borderRadius: 6,
                    borderSkipped: false,
                    maxBarThickness: 44
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: horizontal ? "y" : "x",
                plugins: {
                    legend: { display: false },
                    tooltip: { displayColors: false }
                },
                scales: baseScales
            }
        });
    };

    createBarChart("[data-senior-ratings-chart]", "#d99a3d");
    createBarChart("[data-senior-modules-chart]", "#596f89", true);

    const outcomesCanvas = document.querySelector(
        "[data-senior-outcomes-chart]");
    if (outcomesCanvas) {
        const data = readChartData(outcomesCanvas);
        new window.Chart(outcomesCanvas, {
            type: "doughnut",
            data: {
                labels: data.labels,
                datasets: [{
                    data: data.values,
                    backgroundColor: ["#4f7f74", "#c9873d", "#b45b66"],
                    borderWidth: 0,
                    hoverOffset: 5
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "62%",
                plugins: {
                    legend: {
                        position: "bottom",
                        labels: {
                            boxHeight: 10,
                            boxWidth: 10,
                            color: textColor,
                            padding: 16,
                            usePointStyle: true
                        }
                    }
                }
            }
        });
    }
});
