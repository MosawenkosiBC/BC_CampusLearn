(() => {
    const settingsForm = document.querySelector(
        "[data-application-settings-form]");
    const toggle = settingsForm?.querySelector(
        "[data-applications-toggle]");
    const limitModal = document.querySelector(
        "[data-shortlist-limit-modal]");
    const finalModal = document.querySelector(
        "[data-final-shortlist-modal]");
    const addTutorModal = document.querySelector(
        "[data-add-tutor-modal]");
    const limitForm = limitModal?.querySelector(
        "[data-shortlist-limit-form]");
    const limitInput = limitForm?.querySelector(
        "[name='shortlistLimit']");
    const limitValidation = limitForm?.querySelector(
        "[data-shortlist-limit-validation]");
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

    const clearLimitValidation = () => {
        if (limitValidation) {
            limitValidation.textContent = "";
        }
        limitInput?.removeAttribute("aria-invalid");
    };

    limitInput?.addEventListener("input", clearLimitValidation);

    limitForm?.addEventListener("submit", event => {
        if (event.submitter?.name === "openWithoutTarget") {
            clearLimitValidation();
            return;
        }

        const target = Number(limitInput?.value);
        const isValid = limitInput?.value.trim() &&
            Number.isInteger(target) &&
            target > 0;

        if (isValid) {
            clearLimitValidation();
            return;
        }

        event.preventDefault();
        if (limitValidation) {
            limitValidation.textContent =
                "Enter a whole number greater than 0.";
        }
        limitInput?.setAttribute("aria-invalid", "true");
        limitInput?.focus();
    });

    toggle?.addEventListener("change", () => {
        if (toggle.checked) {
            limitModal?.showModal();
            limitModal?.querySelector("input[type='number']")?.focus();
            return;
        }

        settingsForm.requestSubmit();
    });

    document.querySelectorAll("[data-admin-modal-close]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modal = button.closest("dialog");
                modal?.close();

                if (modal === limitModal && toggle) {
                    toggle.checked = false;
                    clearLimitValidation();
                }
            });
        });

    document.querySelectorAll("[data-final-shortlist]")
        .forEach(button => {
            button.addEventListener("click", () => {
                if (!finalModal) {
                    return;
                }

                const idInput = finalModal.querySelector(
                    "[data-final-candidate-id]");
                const name = finalModal.querySelector(
                    "[data-final-candidate-name]");

                if (idInput) {
                    idInput.value = button.dataset.candidateId ?? "";
                }
                if (name) {
                    name.textContent = button.dataset.candidateName ??
                        "this candidate";
                }

                finalModal.showModal();
            });
        });

    document.querySelector("[data-add-tutor-open]")
        ?.addEventListener("click", () => {
            addTutorModal?.showModal();
        });

    document.addEventListener("click", event => {
        if (studentPicker && !studentPicker.contains(event.target)) {
            if (studentResults) {
                studentResults.hidden = true;
            }
            studentSearch?.setAttribute("aria-expanded", "false");
        }
    });

    [limitModal, finalModal, addTutorModal].forEach(modal => {
        modal?.addEventListener("click", event => {
            if (event.target === modal) {
                modal.close();
                if (modal === limitModal && toggle) {
                    toggle.checked = false;
                    clearLimitValidation();
                }
            }
        });
    });
})();
