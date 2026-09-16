(() => {
    const settingsForm = document.querySelector(
        "[data-application-settings-form]");
    const toggle = settingsForm?.querySelector(
        "[data-applications-toggle]");
    const settingsModal = document.querySelector(
        "[data-application-settings-modal]");
    const thresholdModal = document.querySelector(
        "[data-shortlist-threshold-modal]");
    const reviewResultModal = document.querySelector(
        "[data-review-result-modal]");
    const cycleForm = settingsModal?.querySelector(
        "[data-cycle-settings-form]");
    const cycleOpenDate = cycleForm?.querySelector(
        "[data-cycle-open-date]");
    const cycleCloseDate = cycleForm?.querySelector(
        "[data-cycle-close-date]");
    const cycleValidation = cycleForm?.querySelector(
        "[data-cycle-validation]");
    const addTutorModal = document.querySelector(
        "[data-add-tutor-modal]");
    const studentPicker = document.querySelector("[data-student-picker]");
    const studentSearch = studentPicker?.querySelector(
        "[data-student-search]");
    const studentId = studentPicker?.querySelector("[data-student-id]");
    const studentResults = studentPicker?.querySelector(
        "[data-student-results]");
    const studentOptions = Array.from(studentPicker?.querySelectorAll(
        "[data-student-option]") ?? []);
    const studentEmpty = studentPicker?.querySelector(
        "[data-student-empty]");
    const shortlistReviewModals = Array.from(document.querySelectorAll(
        "[data-shortlist-review-modal]"));

    const filterStudents = () => {
        if (!studentSearch || !studentResults) {
            return;
        }

        const query = studentSearch.value.trim().toLowerCase();

        if (!query) {
            studentResults.hidden = true;
            studentSearch.setAttribute("aria-expanded", "false");
            if (studentEmpty) {
                studentEmpty.hidden = true;
            }
            return;
        }

        let visibleCount = 0;

        studentOptions.forEach(option => {
            const matches = !query ||
                option.dataset.searchText?.includes(query);
            option.hidden = !matches;
            visibleCount += matches ? 1 : 0;
        });

        if (studentEmpty) {
            studentEmpty.hidden = visibleCount > 0;
        }
        studentResults.hidden = false;
        studentSearch.setAttribute("aria-expanded", "true");
    };

    studentSearch?.addEventListener("focus", filterStudents);
    studentSearch?.addEventListener("input", () => {
        if (studentId) {
            studentId.value = "";
        }
        filterStudents();
    });

    studentOptions.forEach(option => {
        option.addEventListener("click", () => {
            if (studentSearch) {
                studentSearch.value = option.textContent.trim();
                studentSearch.setAttribute("aria-expanded", "false");
            }
            if (studentId) {
                studentId.value = option.dataset.studentId ?? "";
            }
            if (studentResults) {
                studentResults.hidden = true;
            }
        });
    });

    studentSearch?.addEventListener("keydown", event => {
        if (event.key === "Escape" && studentResults) {
            studentResults.hidden = true;
            studentSearch.setAttribute("aria-expanded", "false");
        }
    });

    toggle?.addEventListener("change", () => {
        if (toggle.checked) {
            toggle.checked = false;
            settingsModal?.showModal();
            return;
        }
        settingsForm.requestSubmit();
    });

    const syncClosingDate = () => {
        if (!cycleOpenDate || !cycleCloseDate) {
            return;
        }
        cycleCloseDate.min = cycleOpenDate.value;
    };

    cycleOpenDate?.addEventListener("change", syncClosingDate);
    syncClosingDate();

    cycleForm?.addEventListener("submit", event => {
        if (!cycleOpenDate?.value || !cycleCloseDate?.value ||
            cycleCloseDate.value >= cycleOpenDate.value) {
            return;
        }
        event.preventDefault();
        if (cycleValidation) {
            cycleValidation.textContent =
                "The closing date must be on or after the opening date.";
        }
        cycleCloseDate.setAttribute("aria-invalid", "true");
        cycleCloseDate.focus();
    });

    document.querySelectorAll("[data-admin-modal-close]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modal = button.closest("dialog");
                modal?.close();

            });
        });

    document.querySelector("[data-add-tutor-open]")
        ?.addEventListener("click", () => {
            addTutorModal?.showModal();
        });

    document.querySelectorAll("[data-shortlist-view]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modalId = button.dataset.shortlistModalId;
                if (!modalId) {
                    return;
                }

                document.getElementById(modalId)?.showModal();
            });
        });

    document.querySelectorAll("[data-shortlist-action-form]")
        .forEach(form => {
            form.addEventListener("submit", event => {
                if (!event.submitter?.matches("[data-reject-shortlisted]")) {
                    return;
                }

                if (!window.confirm(
                    "Reject this shortlisted applicant? They will not progress to interview.")) {
                    event.preventDefault();
                }
            });
        });

    if (thresholdModal?.dataset.show === "true") {
        thresholdModal.showModal();
    }

    if (settingsModal?.dataset.show === "true") {
        settingsModal.showModal();
    }

    if (reviewResultModal?.dataset.show === "true") {
        reviewResultModal.showModal();
    }

    shortlistReviewModals
        .find(modal => modal.dataset.show === "true")
        ?.showModal();

    document.addEventListener("click", event => {
        if (studentPicker && !studentPicker.contains(event.target)) {
            if (studentResults) {
                studentResults.hidden = true;
            }
            studentSearch?.setAttribute("aria-expanded", "false");
        }
    });

    [addTutorModal, settingsModal, thresholdModal, reviewResultModal,
        ...shortlistReviewModals]
        .forEach(modal => {
        modal?.addEventListener("click", event => {
            if (event.target === modal) {
                modal.close();
            }
        });
        });
})();
