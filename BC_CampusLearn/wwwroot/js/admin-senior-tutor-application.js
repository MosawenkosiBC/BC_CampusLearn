document.addEventListener("DOMContentLoaded", () => {
    const approvalModal = document.getElementById("approve-senior-application-modal");
    const seniorToggle = document.getElementById("approve-senior-toggle");
    const headToggle = document.getElementById("approve-head-toggle");
    const approvalConfirm = document.querySelector("[data-senior-approve-confirm]");
    const approvalSummary = document.getElementById("senior-approval-summary");
    const headConfirmation = document.getElementById("confirm-head-approval-modal");
    let returningFromHeadConfirmation = false;
    let headSelectionConfirmed = false;
    const syncApproval = () => {
        if (!seniorToggle || !headToggle || !approvalConfirm || !approvalSummary) return;
        headToggle.disabled = !seniorToggle.checked;
        if (!seniorToggle.checked) headToggle.checked = false;
        approvalConfirm.disabled = !seniorToggle.checked;
        approvalSummary.hidden = !seniorToggle.checked;
        approvalSummary.textContent = !seniorToggle.checked
            ? ""
            : headToggle.checked
                ? "Senior Tutor and Head of Tutor will both be approved."
                : "Senior Tutor will be approved. Head of Tutor will be declined.";
    };
    seniorToggle?.addEventListener("change", syncApproval);
    headToggle?.addEventListener("change", () => {
        headSelectionConfirmed = false;
        syncApproval();
    });
    document.querySelector("[data-head-selection-confirm]")?.addEventListener("click", () => {
        headSelectionConfirmed = true;
        window.bootstrap.Modal.getOrCreateInstance(headConfirmation).hide();
        decisionForm.requestSubmit(approvalConfirm);
    });
    headConfirmation?.addEventListener("hidden.bs.modal", () => {
        if (headSelectionConfirmed) return;
        window.bootstrap.Modal.getOrCreateInstance(approvalModal).show();
    });
    approvalModal?.addEventListener("show.bs.modal", () => {
        if (returningFromHeadConfirmation) {
            returningFromHeadConfirmation = false;
            return;
        }
        seniorToggle.checked = false;
        headToggle.checked = false;
        headSelectionConfirmed = false;
        syncApproval();
    });
    syncApproval();
    const decisionForm = document.getElementById(
        "senior-application-decision-form");
    const reviewNote = decisionForm?.querySelector("#ReviewNote");
    const reviewNoteValidation = decisionForm?.querySelector(
        "[data-review-note-validation]");
    const rejectionNote = document.getElementById("senior-rejection-note");
    const rejectionError = document.getElementById("senior-rejection-error");

    const clearReviewNoteError = () => {
        if (!reviewNote || !reviewNoteValidation) return;
        reviewNote.removeAttribute("aria-invalid");
        reviewNoteValidation.textContent = "";
        reviewNoteValidation.classList.remove("field-validation-error");
        reviewNoteValidation.classList.add("field-validation-valid");
        rejectionNote?.removeAttribute("aria-invalid");
        if (rejectionError) rejectionError.textContent = "";
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
        rejectionNote?.setAttribute("aria-invalid", "true");
        if (rejectionError) rejectionError.textContent = "Enter a reason before confirming rejection.";
        if (focus) (rejectionNote ?? reviewNote).focus();
        return false;
    };

    reviewNote?.addEventListener("input", () => {
        if (rejectionNote) rejectionNote.value = reviewNote.value;
        if (reviewNote.value.trim()) clearReviewNoteError();
    });
    rejectionNote?.addEventListener("input", () => {
        if (reviewNote) reviewNote.value = rejectionNote.value;
        if (rejectionNote.value.trim()) clearReviewNoteError();
    });

    document.querySelectorAll("[data-senior-reject-trigger]")
        .forEach(trigger => trigger.addEventListener("click", event => {
            event.preventDefault();

            const modalElement = document.getElementById(
                "reject-senior-application-modal");
            if (modalElement && window.bootstrap) {
                if (rejectionNote && reviewNote) rejectionNote.value = reviewNote.value;
                const showRejection = () => {
                    modalElement.addEventListener("shown.bs.modal", () => rejectionNote?.focus(), { once: true });
                    window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
                };
                if (approvalModal?.classList.contains("show")) {
                    approvalModal.addEventListener("hidden.bs.modal", showRejection, { once: true });
                    window.bootstrap.Modal.getOrCreateInstance(approvalModal).hide();
                } else {
                    showRejection();
                }
            }
        }));

    decisionForm?.addEventListener("submit", event => {
        if (event.submitter === approvalConfirm && headToggle?.checked &&
            !headSelectionConfirmed && headConfirmation && window.bootstrap) {
            event.preventDefault();
            returningFromHeadConfirmation = true;
            approvalModal.addEventListener("hidden.bs.modal", () => {
                window.bootstrap.Modal.getOrCreateInstance(headConfirmation).show();
            }, { once: true });
            window.bootstrap.Modal.getOrCreateInstance(approvalModal).hide();
            return;
        }
        const isRejection = event.submitter?.matches(
            "[data-senior-reject-confirm]");
        if (isRejection && rejectionNote && reviewNote) reviewNote.value = rejectionNote.value;
        if (!isRejection || validateRejectionNote()) return;

        event.preventDefault();
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
