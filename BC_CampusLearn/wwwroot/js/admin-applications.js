(() => {
    const rejectRemainingModal = document.querySelector("[data-reject-remaining-modal]");
    document.querySelector("[data-reject-remaining-open]")
        ?.addEventListener("click", () => rejectRemainingModal?.showModal());
    rejectRemainingModal?.addEventListener("click", event => {
        if (event.target === rejectRemainingModal) {
            rejectRemainingModal.close();
        }
    });
    document.querySelector("[data-reject-remaining-form]")
        ?.addEventListener("submit", event => {
            const button = event.currentTarget.querySelector("button[type='submit']");
            if (button) {
                button.disabled = true;
                button.textContent = "Rejecting and deleting…";
            }
        });
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
    const cycleOpenDateDisplay = cycleForm?.querySelector(
        "[data-cycle-open-date-display]");
    const cycleCloseDate = cycleForm?.querySelector(
        "[data-cycle-close-date]");
    const cycleCloseDateDisplay = cycleForm?.querySelector(
        "[data-cycle-close-date-display]");
    const cycleTutorCountdown = cycleForm?.querySelector(
        "[data-cycle-tutor-countdown]");
    const cycleValidation = cycleForm?.querySelector(
        "[data-cycle-validation]");
    const cycleTutorToggle = cycleForm?.querySelector(
        "[data-cycle-tutor-toggle]");
    const cycleTutorSettings = Array.from(cycleForm?.querySelectorAll(
        "[data-cycle-tutor-setting]") ?? []);
    const cycleTutorWindow = cycleForm?.querySelector(
        "[data-cycle-tutor-window]");
    const cycleSeniorToggle = cycleForm?.querySelector(
        "[data-cycle-senior-toggle]");
    const cycleSeniorSettings = Array.from(cycleForm?.querySelectorAll(
        "[data-cycle-senior-setting]") ?? []);
    const cycleSeniorWindow = cycleForm?.querySelector(
        "[data-cycle-senior-window]");
    const cycleSeniorOpenDate = cycleForm?.querySelector(
        "[data-cycle-senior-open-date]");
    const cycleSeniorOpenDateDisplay = cycleForm?.querySelector(
        "[data-cycle-senior-open-date-display]");
    const cycleSeniorCloseDate = cycleForm?.querySelector(
        "[data-cycle-senior-close-date]");
    const cycleSeniorCloseDateDisplay = cycleForm?.querySelector(
        "[data-cycle-senior-close-date-display]");
    const cycleSeniorCountdown = cycleForm?.querySelector(
        "[data-cycle-senior-countdown]");
    const shortlistTargetModal = document.querySelector(
        "[data-shortlist-target-modal]");
    const shortlistTargetInput = document.querySelector(
        "[data-shortlist-target-input]");
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
    const manualModuleCount = manualTutorForm?.querySelector(
        "[data-manual-module-count]");
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
        resetStudentVerification();
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
            verifyStudent(option.dataset.studentId);
        });
    });

    studentSearch?.addEventListener("keydown", event => {
        if (event.key === "Escape" && studentResults) {
            studentResults.hidden = true;
            studentSearch.setAttribute("aria-expanded", "false");
        }
    });

    const syncManualModuleCount = () => {
        const count = manualModules.filter(module =>
            module.querySelector("input[type='checkbox']")?.checked).length;
        if (manualModuleCount) {
            manualModuleCount.textContent = `${count} ${count === 1 ? "module" : "modules"} selected`;
        }
    };

    const syncManualModules = () => {
        const yearOfStudy = Number(manualYear?.value ?? 0);
        const query = manualModuleSearch?.value.trim().toLowerCase() ?? "";
        let eligibleModuleCount = 0;
        let visibleModuleCount = 0;

        manualModules.forEach(module => {
            const isEligible = yearOfStudy >= 1 && yearOfStudy <= 4 &&
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

        syncManualModuleCount();

        if (manualModuleEmpty) {
            manualModuleEmpty.hidden = visibleModuleCount > 0;
            if (yearOfStudy < 1 || yearOfStudy > 4) {
                manualModuleEmpty.textContent =
                    "Select a student to see modules eligible for their year of study.";
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
    manualModules.forEach(module => {
        module.querySelector("input[type='checkbox']")?.addEventListener("change", syncManualModuleCount);
    });
    syncManualModules();

    const studentDetailsUrl = manualTutorForm?.dataset.studentDetailsUrl;
    const verificationStatus = manualTutorForm?.querySelector("[data-student-verification-status]");
    const manualCampus = manualTutorForm?.querySelector("[name='ManualTutor.CampusOfStudy']");
    const addTutorSubmit = manualTutorForm?.querySelector("button[type='submit']");
    let verifiedStudentId = null;
    let verificationRequest = null;

    const resetStudentVerification = () => {
        if (!studentDetailsUrl) return;
        verificationRequest?.abort();
        verifiedStudentId = null;
        if (addTutorSubmit) addTutorSubmit.disabled = true;
        if (manualProgramme) { manualProgramme.value = ""; manualProgramme.disabled = true; }
        if (manualYear) { manualYear.value = ""; manualYear.readOnly = true; }
        if (manualCampus) { manualCampus.value = ""; manualCampus.disabled = true; }
        if (verificationStatus) verificationStatus.textContent = "Select a student to verify their details.";
        syncManualModules();
    };

    const verifyStudent = async studentId => {
        if (!studentDetailsUrl) return;
        resetStudentVerification();
        const request = new AbortController();
        verificationRequest = request;
        if (verificationStatus) verificationStatus.textContent = "Checking student details…";
        try {
            const url = new URL(studentDetailsUrl, window.location.origin);
            url.searchParams.set("studentId", studentId);
            const response = await fetch(url, { signal: request.signal, cache: "no-store", headers: { Accept: "application/json" } });
            const details = await response.json();
            if (request.signal.aborted) return;
            if (!response.ok) throw new Error(details.error || "Student information could not be verified. Please try again.");
            if (manualProgramme) manualProgramme.value = String(details.programmeId);
            if (manualYear) manualYear.value = String(details.yearOfStudy);
            if (manualCampus) manualCampus.value = details.campus;
            if (studentSearch) studentSearch.value = `${details.displayName} (${details.studentNumber})`;
            verifiedStudentId = String(details.studentId);
            if (verificationStatus) verificationStatus.textContent = "";
            if (addTutorSubmit) addTutorSubmit.disabled = false;
            syncManualModules();
        } catch (error) {
            if (request.signal.aborted) return;
            if (verificationStatus) verificationStatus.textContent = error instanceof SyntaxError || error instanceof TypeError
                ? "Student information is temporarily unavailable. Please try again."
                : error.message;
        }
    };

    const manualErrors = Array.from(manualTutorForm?.querySelectorAll("[data-manual-error-for]") ?? []);
    const manualFormError = manualTutorForm?.querySelector("[data-manual-form-error]");
    let manualTutorSubmitting = false;

    manualTutorForm?.addEventListener("input", event => {
        const field = event.target.name;
        const error = manualErrors.find(item => item.dataset.manualErrorFor === field);
        if (error) error.textContent = "";
        event.target.removeAttribute("aria-invalid");
    });

    manualTutorForm?.addEventListener("submit", async event => {
        event.preventDefault();
        if (manualTutorSubmitting) return;
        manualErrors.forEach(error => { error.textContent = ""; });
        manualTutorForm.querySelectorAll("[aria-invalid]").forEach(input => input.removeAttribute("aria-invalid"));
        if (manualFormError) { manualFormError.textContent = ""; manualFormError.hidden = true; }
        if (studentDetailsUrl && verifiedStudentId !== manualTutorForm.querySelector("[data-student-choice]:checked")?.value) {
            const error = manualErrors.find(item => item.dataset.manualErrorFor === "ManualTutor.BcUserId");
            if (error) error.textContent = "Select a student and wait for their details to be verified.";
            studentSearch?.focus();
            return;
        }

        manualTutorSubmitting = true;
        if (addTutorSubmit) addTutorSubmit.disabled = true;
        try {
            const response = await fetch(manualTutorForm.action, {
                method: "POST",
                body: new FormData(manualTutorForm),
                headers: { Accept: "application/json" }
            });
            const result = await response.json();
            if (response.ok && result.succeeded) {
                window.location.reload();
                return;
            }
            if (!result.errors) throw new Error("Unable to add the tutor. Please try again.");
            let firstInput = null;
            for (const [field, message] of Object.entries(result.errors)) {
                const error = manualErrors.find(item => item.dataset.manualErrorFor === field);
                if (error) error.textContent = message;
                const input = field === "ManualTutor.BcUserId" ? studentSearch
                    : Array.from(manualTutorForm.elements).find(element => element.name === field && !element.disabled);
                input?.setAttribute("aria-invalid", "true");
                firstInput ??= input;
            }
            firstInput?.focus();
        } catch {
            if (manualFormError) {
                manualFormError.textContent = "Unable to add the tutor. Please try again.";
                manualFormError.hidden = false;
            }
        } finally {
            manualTutorSubmitting = false;
            if (addTutorSubmit) addTutorSubmit.disabled = false;
        }
    });
    resetStudentVerification();

    document.querySelector("[data-open-application-settings]")
        ?.addEventListener("click", () => settingsModal?.showModal());

    settingsModal?.addEventListener("click", event => {
        if (event.target !== settingsModal) return;
        event.preventDefault();
        event.stopPropagation();
    });

    settingsModal?.addEventListener("cancel", event => {
        event.preventDefault();
    });

    const syncTutorApplicationSettings = () => {
        const enabled = cycleTutorToggle?.checked ?? false;
        if (cycleTutorWindow) cycleTutorWindow.hidden = !enabled;
        cycleTutorSettings.forEach(control => {
            control.disabled = !enabled;
            control.required = enabled;
        });
    };

    cycleTutorToggle?.addEventListener(
        "change",
        () => {
            syncTutorApplicationSettings();
            if (cycleTutorToggle.checked) {
                shortlistTargetModal?.showModal();
            }
        });
    syncTutorApplicationSettings();

    const syncSeniorApplicationSettings = () => {
        const enabled = cycleSeniorToggle?.checked ?? false;
        if (cycleSeniorWindow) cycleSeniorWindow.hidden = !enabled;
        cycleSeniorSettings.forEach(control => {
            control.disabled = !enabled;
            control.required = enabled;
        });
    };

    cycleSeniorToggle?.addEventListener(
        "change",
        syncSeniorApplicationSettings);
    syncSeniorApplicationSettings();

    document.querySelector("[data-shortlist-target-cancel]")
        ?.addEventListener("click", () => {
            if (cycleTutorToggle) cycleTutorToggle.checked = false;
            syncTutorApplicationSettings();
            shortlistTargetModal?.close();
        });
    document.querySelector("[data-shortlist-target-clear]")
        ?.addEventListener("click", () => {
            if (shortlistTargetInput) shortlistTargetInput.value = "";
            shortlistTargetModal?.close();
        });
    document.querySelector("[data-shortlist-target-confirm]")
        ?.addEventListener("click", () => {
            if (shortlistTargetInput?.value &&
                !shortlistTargetInput.checkValidity()) {
                shortlistTargetInput.reportValidity();
                return;
            }
            shortlistTargetModal?.close();
        });

    const formatDateInput = value => {
        const digits = value.replace(/\D/g, "").slice(0, 8);
        if (digits.length <= 2) return digits;
        if (digits.length <= 4) {
            return `${digits.slice(0, 2)}/${digits.slice(2)}`;
        }
        return `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
    };

    const parseDisplayDate = value => {
        const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
        if (!match) return "";

        const day = Number(match[1]);
        const month = Number(match[2]);
        const year = Number(match[3]);
        const date = new Date(Date.UTC(year, month - 1, day));
        if (date.getUTCFullYear() !== year ||
            date.getUTCMonth() !== month - 1 ||
            date.getUTCDate() !== day) {
            return "";
        }

        return `${String(year).padStart(4, "0")}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
    };

    const formatIsoDate = value => {
        const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
        return match ? `${match[3]}/${match[2]}/${match[1]}` : "";
    };

    const bindDisplayDate = (display, hidden, onUpdate) => {
        if (!display || !hidden) return;

        const update = () => {
            display.value = formatDateInput(display.value);
            const isoDate = parseDisplayDate(display.value);
            hidden.value = isoDate;
            display.setCustomValidity(
                display.value && !isoDate
                    ? "Enter a valid date in dd/mm/yyyy format."
                    : "");
            display.toggleAttribute(
                "aria-invalid",
                Boolean(display.value && !isoDate));
            onUpdate();
        };

        display.addEventListener("input", update);
        display.addEventListener("blur", update);
        hidden.addEventListener("change", () => {
            display.value = formatIsoDate(hidden.value);
            display.setCustomValidity("");
            display.removeAttribute("aria-invalid");
            onUpdate();
        });
    };

    document.querySelectorAll("[data-date-picker-target]")
        .forEach(button => {
            button.addEventListener("click", () => {
                const picker = document.getElementById(
                    button.dataset.datePickerTarget);
                if (!picker || picker.disabled) return;

                if (typeof picker.showPicker === "function") {
                    picker.showPicker();
                } else {
                    picker.click();
                }
            });
        });

    const syncApplicationCountdown = (openDate, closeDate, countdown) => {
        if (!countdown || !openDate?.value || !closeDate?.value) {
            if (countdown) {
                countdown.textContent = "";
                countdown.hidden = true;
            }
            return;
        }

        const toUtcDay = value => {
            const [year, month, day] = value.split("-").map(Number);
            return Date.UTC(year, month - 1, day);
        };
        const now = new Date();
        const today = Date.UTC(
            now.getUTCFullYear(),
            now.getUTCMonth(),
            now.getUTCDate());
        const openingDay = toUtcDay(openDate.value);
        const closingDay = toUtcDay(closeDate.value);
        const millisecondsPerDay = 24 * 60 * 60 * 1000;
        const daysUntilOpening = Math.round(
            (openingDay - today) / millisecondsPerDay);
        const daysRemaining = Math.round(
            (closingDay - today) / millisecondsPerDay);

        if (daysUntilOpening > 0) {
            countdown.textContent = daysUntilOpening === 1
                ? "Opens tomorrow"
                : `Opens in ${daysUntilOpening} days`;
        } else if (daysRemaining < 0) {
            countdown.textContent = "Closed";
        } else if (daysRemaining === 0) {
            countdown.textContent = "Closes today";
        } else {
            countdown.textContent = daysRemaining === 1
                ? "1 day remaining"
                : `${daysRemaining} days remaining`;
        }
        countdown.hidden = false;
    };

    const syncClosingDate = () => {
        if (!cycleOpenDate || !cycleCloseDate) {
            return;
        }
        cycleCloseDate.min = cycleOpenDate.value;
        syncApplicationCountdown(
            cycleOpenDate,
            cycleCloseDate,
            cycleTutorCountdown);
    };

    bindDisplayDate(
        cycleOpenDateDisplay,
        cycleOpenDate,
        syncClosingDate);
    bindDisplayDate(
        cycleCloseDateDisplay,
        cycleCloseDate,
        syncClosingDate);
    syncClosingDate();

    const syncSeniorClosingDate = () => {
        if (!cycleSeniorOpenDate || !cycleSeniorCloseDate) return;
        cycleSeniorCloseDate.min = cycleSeniorOpenDate.value;
        syncApplicationCountdown(
            cycleSeniorOpenDate,
            cycleSeniorCloseDate,
            cycleSeniorCountdown);
    };

    bindDisplayDate(
        cycleSeniorOpenDateDisplay,
        cycleSeniorOpenDate,
        syncSeniorClosingDate);
    bindDisplayDate(
        cycleSeniorCloseDateDisplay,
        cycleSeniorCloseDate,
        syncSeniorClosingDate);
    syncSeniorClosingDate();

    cycleForm?.addEventListener("submit", event => {
        const invalidTutorDates = cycleTutorToggle?.checked &&
            (!cycleOpenDate?.value || !cycleCloseDate?.value ||
                cycleCloseDate.value < cycleOpenDate.value);
        const invalidSeniorDates = cycleSeniorToggle?.checked &&
            (!cycleSeniorOpenDate?.value || !cycleSeniorCloseDate?.value ||
                cycleSeniorCloseDate.value < cycleSeniorOpenDate.value);
        if (!invalidTutorDates && !invalidSeniorDates) return;

        event.preventDefault();
        const invalidCloseDate = invalidTutorDates
            ? (!cycleOpenDate?.value
                ? cycleOpenDateDisplay
                : cycleCloseDateDisplay)
            : (!cycleSeniorOpenDate?.value
                ? cycleSeniorOpenDateDisplay
                : cycleSeniorCloseDateDisplay);
        if (cycleValidation) {
            cycleValidation.textContent = invalidTutorDates
                ? "Enter valid Become a Tutor opening and closing dates."
                : "Enter valid Senior Tutor application opening and closing dates.";
        }
        invalidCloseDate?.setAttribute("aria-invalid", "true");
        invalidCloseDate?.focus();
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
Mzala Connect Tutor Team`
        },
        availability: {
            subject: "Confirm your tutor interview availability",
            body: `Dear ${studentName},

Your tutor application has progressed to the interview preparation stage. Please reply with your availability for an interview during the following period:

[Add proposed dates or times]

Once we receive your availability, we will confirm the interview details.

Kind regards,
Mzala Connect Tutor Team`
        },
        information: {
            subject: "Additional information required for your tutor application",
            body: `Dear ${studentName},

We are currently preparing your shortlisted tutor application for the interview stage. Before we proceed, please provide the following information:

[List the required information]

Please reply by [add deadline].

Kind regards,
Mzala Connect Tutor Team`
        },
        followup: {
            subject: "Thank you for attending your tutor interview",
            body: `Dear ${studentName},

Thank you for taking the time to attend your tutor interview. We appreciate the opportunity to learn more about your experience and interest in tutoring.

We will contact you once the interview review has been completed.

Kind regards,
Mzala Connect Tutor Team`
        },
        custom: {
            subject: "",
            body: `Dear ${studentName},

[Write your message here]

Kind regards,
Mzala Connect Tutor Team`
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
                const returnModal = document.getElementById(returnModalId);
                if (returnModal && !returnModal.open) {
                    returnModal.showModal();
                }
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
Mzala Connect Tutor Team`;
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

                const parentModal = button.closest("dialog");
                if (!parentModal?.matches(".shortlist-meeting-modal")) {
                    parentModal?.close();
                }
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
