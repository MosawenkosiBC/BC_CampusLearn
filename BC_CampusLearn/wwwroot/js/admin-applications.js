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
    const manualTutorForm = addTutorModal?.querySelector(
        "[data-manual-tutor-form]");
    const manualProgramme = manualTutorForm?.querySelector(
        "[data-manual-programme]");
    const manualYear = manualTutorForm?.querySelector(
        "[data-manual-year]");
    const manualModuleSearch = manualTutorForm?.querySelector(
        "[data-manual-module-search]");
    const manualModules = Array.from(manualTutorForm?.querySelectorAll(
        "[data-manual-module]") ?? []);
    const manualModuleEmpty = manualTutorForm?.querySelector(
        "[data-manual-module-empty]");
    const studentPicker = document.querySelector("[data-student-picker]");
    const studentSearch = studentPicker?.querySelector(
        "[data-student-search]");
    const studentResults = studentPicker?.querySelector(
        "[data-student-results]");
    const studentOptions = Array.from(studentPicker?.querySelectorAll(
        "[data-student-option]") ?? []);
    const studentChoices = Array.from(studentPicker?.querySelectorAll(
        "[data-student-choice]") ?? []);
    const studentEmpty = studentPicker?.querySelector(
        "[data-student-empty]");
    const shortlistReviewModals = Array.from(document.querySelectorAll(
        "[data-shortlist-review-modal]"));
    const interviewProcessModals = Array.from(document.querySelectorAll(
        "[data-interview-process-modal]"));
    const interviewRoomModals = Array.from(document.querySelectorAll(
        "[data-interview-room-modal]"));
    const candidateEmailModals = Array.from(document.querySelectorAll(
        "[data-candidate-email-modal]"));
    const interviewScheduleModals = Array.from(document.querySelectorAll(
        "[data-interview-schedule-modal]"));
    const shortlistRejectModals = Array.from(document.querySelectorAll(
        "[data-shortlist-reject-modal]"));
    const interviewConfirmModals = Array.from(document.querySelectorAll(
        "[data-interview-confirm-modal]"));

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
        studentChoices.forEach(choice => {
            choice.checked = false;
        });
        filterStudents();
    });

    studentOptions.forEach(option => {
        option.addEventListener("click", () => {
            if (studentSearch) {
                studentSearch.value = option.textContent.trim();
                studentSearch.setAttribute("aria-expanded", "false");
            }
            const choice = option.querySelector("[data-student-choice]");
            if (choice) {
                choice.checked = true;
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

    const syncManualModules = () => {
        const programmeId = manualProgramme?.value ?? "";
        const yearOfStudy = Number(manualYear?.value ?? 0);
        const query = manualModuleSearch?.value.trim().toLowerCase() ?? "";
        let eligibleModuleCount = 0;
        let visibleModuleCount = 0;

        manualModules.forEach(module => {
            const isEligible = Boolean(programmeId) &&
                yearOfStudy >= 1 && yearOfStudy <= 4 &&
                module.dataset.programmeId === programmeId &&
                Number(module.dataset.moduleYear) <= yearOfStudy;
            const matchesSearch = !query ||
                module.dataset.moduleSearch?.includes(query);

            module.hidden = !isEligible || !matchesSearch;
            if (!isEligible) {
                const checkbox = module.querySelector("input[type='checkbox']");
                if (checkbox) {
                    checkbox.checked = false;
                }
            } else {
                eligibleModuleCount += 1;
            }
            if (isEligible && matchesSearch) {
                visibleModuleCount += 1;
            }
        });

        if (manualModuleEmpty) {
            manualModuleEmpty.hidden = visibleModuleCount > 0;
            if (!programmeId || yearOfStudy < 1 || yearOfStudy > 4) {
                manualModuleEmpty.textContent =
                    "Choose a programme and year first.";
            } else if (eligibleModuleCount > 0 && query) {
                manualModuleEmpty.textContent =
                    "No modules match your search.";
            } else {
                manualModuleEmpty.textContent =
                    "No eligible modules were found.";
            }
        }
    };

    manualProgramme?.addEventListener("change", syncManualModules);
    manualYear?.addEventListener("input", syncManualModules);
    manualModuleSearch?.addEventListener("input", syncManualModules);
    syncManualModules();

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

    const getEmailTemplates = studentName => ({
        interview: {
            subject: "Tutor application interview invitation",
            body: `Dear ${studentName},

We are pleased to let you know that your tutor application has been shortlisted. We would like to invite you to an interview.

Interview details:
Date: [Add date]
Time: [Add time]
Duration: [Add duration]
Meeting link: [Add meeting link]

Please reply to confirm that you are available.

Kind regards,
BC CampusLearn Tutor Team`
        },
        availability: {
            subject: "Confirm your tutor interview availability",
            body: `Dear ${studentName},

Your tutor application has progressed to the interview preparation stage. Please reply with your availability for an interview during the following period:

[Add proposed dates or times]

Once we receive your availability, we will confirm the interview details.

Kind regards,
BC CampusLearn Tutor Team`
        },
        information: {
            subject: "Additional information required for your tutor application",
            body: `Dear ${studentName},

We are currently preparing your shortlisted tutor application for the interview stage. Before we proceed, please provide the following information:

[List the required information]

Please reply by [add deadline].

Kind regards,
BC CampusLearn Tutor Team`
        },
        followup: {
            subject: "Thank you for attending your tutor interview",
            body: `Dear ${studentName},

Thank you for taking the time to attend your tutor interview. We appreciate the opportunity to learn more about your experience and interest in tutoring.

We will contact you once the interview review has been completed.

Kind regards,
BC CampusLearn Tutor Team`
        },
        custom: {
            subject: "",
            body: `Dear ${studentName},

[Write your message here]

Kind regards,
BC CampusLearn Tutor Team`
        }
    });

    candidateEmailModals.forEach(modal => {
        const composer = modal.querySelector("[data-email-composer]");
        const templateSelect = composer?.querySelector(
            "[data-email-template]");
        const subject = composer?.querySelector("[data-email-subject]");
        const body = composer?.querySelector("[data-email-body]");
        const characterCount = composer?.querySelector(
            "[data-email-character-count]");
        const scheduleButton = composer?.querySelector(
            "[data-interview-schedule-open]");
        const templates = getEmailTemplates(
            modal.dataset.studentName ?? "Student");

        const updateCharacterCount = () => {
            if (body && characterCount) {
                characterCount.textContent = `${body.value.length} / 5000`;
            }
        };

        templateSelect?.addEventListener("change", () => {
            const template = templates[templateSelect.value];
            if (!template) {
                return;
            }
            if (subject) {
                subject.value = template.subject;
            }
            if (body) {
                body.value = template.body;
            }
            if (scheduleButton) {
                scheduleButton.hidden = templateSelect.value !== "interview";
            }
            updateCharacterCount();
        });

        body?.addEventListener("input", updateCharacterCount);
        updateCharacterCount();

        modal.querySelectorAll("[data-communication-close]")
            .forEach(button => {
                button.addEventListener("click", () => modal.close());
            });

        modal.addEventListener("close", () => {
            if (modal.dataset.skipReturn === "true") {
                delete modal.dataset.skipReturn;
                return;
            }
            const returnModalId = modal.dataset.returnModalId;
            if (returnModalId) {
                document.getElementById(returnModalId)?.showModal();
            }
        });

        modal.addEventListener("click", event => {
            if (event.target === modal) {
                modal.close();
            }
        });
    });

    interviewScheduleModals.forEach(modal => {
        const interviewDate = modal.querySelector("[data-interview-date]");
        const interviewTime = modal.querySelector("[data-interview-time]");
        const interviewDuration = modal.querySelector(
            "[data-interview-duration]");
        const interviewLocation = modal.querySelector(
            "[data-interview-location]");
        const saveButton = modal.querySelector(
            "[data-populate-interview-email]");

        modal.querySelectorAll("[data-interview-schedule-close]")
            .forEach(button => {
                button.addEventListener("click", () => modal.close());
            });

        interviewTime?.addEventListener("input", () => {
            interviewTime.setCustomValidity("");
        });

        interviewTime?.addEventListener("blur", () => {
            if (/^\d{4}$/.test(interviewTime.value)) {
                interviewTime.value = `${interviewTime.value.slice(0, 2)}:${interviewTime.value.slice(2)}`;
            }
        });

        saveButton?.addEventListener("click", () => {
            const emailModalId = modal.dataset.emailModalId;
            const emailModal = emailModalId
                ? document.getElementById(emailModalId)
                : null;
            const composer = emailModal?.querySelector(
                "[data-email-composer]");
            const templateSelect = composer?.querySelector(
                "[data-email-template]");
            const subject = composer?.querySelector("[data-email-subject]");
            const body = composer?.querySelector("[data-email-body]");
            const studentName = emailModal?.dataset.studentName ?? "Student";
            const templates = getEmailTemplates(studentName);
            const rawDate = interviewDate?.value ?? "";
            const formattedDate = rawDate
                ? new Date(`${rawDate}T00:00:00`).toLocaleDateString(
                    "en-ZA",
                    { day: "2-digit", month: "long", year: "numeric" })
                : "[Add date]";
            const enteredTime = interviewTime?.value.trim() ?? "";
            if (enteredTime &&
                !/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(enteredTime)) {
                interviewTime?.setCustomValidity(
                    "Enter time in 24-hour HH:mm format, for example 14:30.");
                interviewTime?.reportValidity();
                return;
            }
            const time = enteredTime || "[Add time]";
            const duration = interviewDuration?.value
                ? interviewDuration.selectedOptions[0]?.textContent.trim()
                : "[Add duration]";
            const location = interviewLocation?.value.trim() ||
                "[Add meeting link]";

            if (templateSelect) {
                templateSelect.value = "interview";
            }
            if (subject) {
                subject.value = templates.interview.subject;
            }
            if (body) {
                body.value = `Dear ${studentName},

We are pleased to let you know that your tutor application has been shortlisted. We would like to invite you to an interview.

Interview details:
Date: ${formattedDate}
Time: ${time}
Duration: ${duration}
Meeting link: ${location}

Please reply to confirm that you are available.

Kind regards,
BC CampusLearn Tutor Team`;
                body.dispatchEvent(new Event("input"));
            }

            modal.close();
        });

        modal.addEventListener("close", () => {
            const returnModalId = modal.dataset.returnModalId;
            if (returnModalId) {
                document.getElementById(returnModalId)?.showModal();
            }
        });

        modal.addEventListener("click", event => {
            if (event.target === modal) {
                modal.close();
            }
        });
    });

    document.querySelectorAll("[data-interview-schedule-open]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modalId = button.dataset.scheduleModalId;
                const scheduleModal = modalId
                    ? document.getElementById(modalId)
                    : null;
                const emailModal = button.closest("dialog");
                if (!scheduleModal || !emailModal) {
                    return;
                }

                emailModal.dataset.skipReturn = "true";
                emailModal.close();
                scheduleModal.showModal();
            });
        });

    shortlistRejectModals.forEach(modal => {
        modal.querySelectorAll("[data-shortlist-reject-close]")
            .forEach(button => {
                button.addEventListener("click", () => modal.close());
            });

        modal.addEventListener("close", () => {
            const returnModalId = modal.dataset.returnModalId;
            if (returnModalId) {
                document.getElementById(returnModalId)?.showModal();
            }
        });

        modal.addEventListener("click", event => {
            if (event.target === modal) {
                modal.close();
            }
        });
    });

    document.querySelectorAll("[data-shortlist-reject-open]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modalId = button.dataset.rejectModalId;
                const rejectModal = modalId
                    ? document.getElementById(modalId)
                    : null;
                if (!rejectModal) {
                    return;
                }

                button.closest("dialog")?.close();
                rejectModal.showModal();
            });
        });

    interviewConfirmModals.forEach(modal => {
        modal.querySelectorAll("[data-interview-confirm-close]")
            .forEach(button => {
                button.addEventListener("click", () => modal.close());
            });

        modal.addEventListener("close", () => {
            const returnModalId = modal.dataset.returnModalId;
            if (returnModalId) {
                document.getElementById(returnModalId)?.showModal();
            }
        });

        modal.addEventListener("click", event => {
            if (event.target === modal) {
                modal.close();
            }
        });
    });

    document.querySelectorAll("[data-interview-confirm-open]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modalId = button.dataset.interviewConfirmModalId;
                const confirmModal = modalId
                    ? document.getElementById(modalId)
                    : null;
                if (!confirmModal) {
                    return;
                }

                button.closest("dialog")?.close();
                confirmModal.showModal();
            });
        });

    document.querySelectorAll("[data-communicate-open]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modalId = button.dataset.communicationModalId;
                const emailModal = modalId
                    ? document.getElementById(modalId)
                    : null;
                if (!emailModal) {
                    return;
                }

                button.closest("dialog")?.close();
                emailModal.showModal();
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

    document.querySelectorAll("[data-interview-process-open]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modalId = button.dataset.interviewProcessModalId;
                const modal = modalId
                    ? document.getElementById(modalId)
                    : null;
                modal?.showModal();
            });
        });

    document.querySelectorAll("[data-interview-room-open]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const modalId = button.dataset.interviewRoomModalId;
                const roomModal = modalId
                    ? document.getElementById(modalId)
                    : null;
                if (!roomModal) {
                    return;
                }

                button.closest("dialog")?.close();
                roomModal.showModal();
                roomModal.querySelector("[data-interview-notes]")?.focus();
            });
        });

    interviewRoomModals.forEach(modal => {
        const notes = modal.querySelector("[data-interview-notes]");
        const count = notes?.dataset.characterCountId
            ? document.getElementById(notes.dataset.characterCountId)
            : null;
        const updateCount = () => {
            if (notes && count) {
                count.textContent = `${notes.value.length} / 4000`;
            }
        };

        notes?.addEventListener("input", updateCount);
        updateCount();

        modal.querySelector("[data-interview-room-back]")
            ?.addEventListener("click", () => {
                const parentId = modal.dataset.parentModalId;
                const parentModal = parentId
                    ? document.getElementById(parentId)
                    : null;
                modal.close();
                parentModal?.showModal();
            });
    });

    interviewRoomModals
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
        ...shortlistReviewModals, ...interviewProcessModals,
        ...interviewRoomModals]
        .forEach(modal => {
        modal?.addEventListener("click", event => {
            if (event.target === modal) {
                modal.close();
            }
        });
        });
})();
