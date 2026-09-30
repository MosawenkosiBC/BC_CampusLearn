(() => {
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
        const pageButton = [...form.querySelectorAll(".campus-pagination__page")]
            .find((button) => button.textContent.trim() === String(pageNumber));
        pageButton?.click();

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
