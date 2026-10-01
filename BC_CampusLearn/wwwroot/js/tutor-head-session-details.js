(() => {
    const aiAssessmentModal = document.getElementById("ai-assessment-modal");
    if (aiAssessmentModal?.dataset.openOnLoad === "true" && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(aiAssessmentModal).show();
    }

    const aiAssessmentForm = document.querySelector(
        "[data-ai-assessment-form]");
    const aiAssessmentButton = aiAssessmentForm?.querySelector(
        "button[type='submit']");
    const aiAssessmentSpinner = aiAssessmentButton?.querySelector(
        "[data-ai-assessment-spinner]");
    const aiAssessmentIcon = aiAssessmentButton?.querySelector(
        "[data-ai-assessment-icon]");
    const aiAssessmentLabel = aiAssessmentButton?.querySelector(
        "[data-ai-assessment-label]");
    const aiAssessmentInitiallyDisabled = aiAssessmentButton?.disabled ?? false;
    aiAssessmentForm?.addEventListener("submit", () => {
        if (aiAssessmentButton) {
            aiAssessmentButton.disabled = true;
            aiAssessmentButton.setAttribute("aria-busy", "true");
        }
        if (aiAssessmentSpinner) {
            aiAssessmentSpinner.hidden = false;
        }
        if (aiAssessmentIcon) {
            aiAssessmentIcon.hidden = true;
        }
        if (aiAssessmentLabel) {
            aiAssessmentLabel.textContent = "Generating assessment...";
        }
    });

    window.addEventListener("pageshow", () => {
        if (aiAssessmentButton) {
            aiAssessmentButton.disabled = aiAssessmentInitiallyDisabled;
            aiAssessmentButton.removeAttribute("aria-busy");
        }
        if (aiAssessmentSpinner) {
            aiAssessmentSpinner.hidden = true;
        }
        if (aiAssessmentIcon) {
            aiAssessmentIcon.hidden = false;
        }
        if (aiAssessmentLabel) {
            aiAssessmentLabel.textContent = "Generate AI assessment";
        }
    });

    const form = document.querySelector("[data-tutor-head-review-form]");
    if (!form) {
        return;
    }

    const questions = [...form.querySelectorAll("[data-option-question]")];

    const clearQuestionError = (question) => {
        question.classList.remove("is-invalid");
        const error = question.querySelector("[data-option-error]");
        if (error) {
            error.hidden = true;
        }
    };

    const showQuestionError = (question) => {
        question.classList.add("is-invalid");
        let error = question.querySelector("[data-option-error]");
        if (!error) {
            error = document.createElement("p");
            error.className = "tutor-head-question-error";
            error.dataset.optionError = "";
            error.textContent = "Select an option before saving the review.";
            question.append(error);
        }
        error.hidden = false;
    };

    questions.forEach((question) => {
        question.addEventListener("change", () => clearQuestionError(question));
    });

    form.addEventListener("submit", (event) => {
        const unanswered = questions.filter((question) =>
            !question.querySelector('input[type="radio"]:checked'));

        questions.forEach(clearQuestionError);
        if (unanswered.length === 0) {
            return;
        }

        event.preventDefault();
        unanswered.forEach(showQuestionError);

        const firstQuestion = unanswered[0];
        const rowIndex = [...form.querySelectorAll("[data-pagination-row]")]
            .indexOf(firstQuestion);
        const pageNumber = Math.floor(rowIndex / 5) + 1;
        const pagination = form.querySelector(".admin-session-review-pagination");
        const pageStatus = pagination?.querySelector("[data-review-page-status]");
        const currentPage = Number.parseInt(
            pageStatus?.textContent.match(/Page\s+(\d+)/i)?.[1] ?? "1",
            10);
        const direction = pageNumber > currentPage
            ? pagination?.querySelector("[data-review-next]")
            : pagination?.querySelector("[data-review-previous]");

        for (let page = currentPage; page !== pageNumber;) {
            direction?.click();
            page += pageNumber > currentPage ? 1 : -1;
        }

        firstQuestion.scrollIntoView({
            behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches
                ? "auto"
                : "smooth",
            block: "center"
        });
        const firstLegend = firstQuestion.querySelector("legend");
        if (firstLegend) {
            firstLegend.tabIndex = -1;
            firstLegend.focus({ preventScroll: true });
        }
    });
})();
