(() => {
    const deleteModal = document.getElementById("survey-delete-modal");
    if (deleteModal) {
        const deleteForm = deleteModal.querySelector("form");
        const confirmDelete = deleteForm.querySelector('button[type="submit"]');
        deleteModal.addEventListener("show.bs.modal", event => {
            const survey = event.relatedTarget?.dataset;
            if (!survey?.surveyId || !survey.surveyRevision) {
                event.preventDefault();
                return;
            }

            deleteForm.elements.namedItem("surveyId").value = survey.surveyId;
            deleteForm.elements.namedItem("revision").value = survey.surveyRevision;
            deleteModal.querySelector("[data-survey-delete-name]").textContent = survey.surveyTitle;
            confirmDelete.disabled = false;
        });
        deleteModal.addEventListener("hidden.bs.modal", () => {
            confirmDelete.disabled = true;
        });
    }

    const editor = document.querySelector("[data-survey-editor]");
    if (!editor) return;

    const payload = editor.querySelector("[data-survey-questions]");
    const builder = editor.querySelector("[data-survey-builder]");
    const list = editor.querySelector("[data-question-list]");
    const template = editor.querySelector("[data-question-template]");
    const entryTemplate = editor.querySelector("[data-entry-template]");
    const picker = editor.querySelector("[data-question-picker]");
    const limit = editor.querySelector("[data-question-limit]");
    const status = editor.querySelector("[data-editor-status]");
    const submitButtons = [...editor.querySelectorAll('button[type="submit"]')];
    const missingQuestionModal = document.getElementById("survey-missing-question-modal");
    const missingQuestionWarning = missingQuestionModal && window.bootstrap
        ? window.bootstrap.Modal.getOrCreateInstance(missingQuestionModal)
        : null;
    let submitting = false;
    let activeQuestion = null;
    let revealingInvalidQuestion = false;
    const maxItems = Number(editor.dataset.maxItems);
    const maxEntries = Number(editor.dataset.maxEntries);
    const typeButtons = [...picker.querySelectorAll("[data-question-type]")];
    const labels = new Map(typeButtons.map(button => [button.dataset.questionType, button.textContent.trim()]));
    const field = (card, name) => card.querySelector(`[data-field="${name}"]`);
    const actionButton = (card, action) => card.querySelector(`[data-action="${action}"]`);
    const entryInputs = (card, kind) => [...card.querySelectorAll(`[data-entry-kind="${kind}"]`)];
    const entries = (card, kind) => entryInputs(card, kind).map(input => input.value);
    const defaults = (label, count) => Array.from({ length: count }, (_, index) => `${label} ${index + 1}`);

    function updateSummary(card) {
        const prompt = field(card, "prompt");
        card.querySelector("[data-summary-prompt]").textContent = prompt.value.trim() || prompt.placeholder;
        card.querySelector("[data-summary-required]").hidden =
            card.dataset.type === "Section" || !field(card, "required").checked;
    }

    function selectQuestion(card) {
        if (activeQuestion === card) return;

        if (activeQuestion) {
            updateSummary(activeQuestion);
            activeQuestion.classList.remove("is-editing");
            activeQuestion.querySelector(".survey-question-body").inert = true;
        }

        activeQuestion = card;
        if (card) {
            card.classList.add("is-editing");
            card.querySelector(".survey-question-body").inert = false;
        }
    }

    function refreshOrder() {
        const atLimit = list.children.length >= maxItems;
        [...list.children].forEach((card, index) => {
            card.querySelector("[data-question-heading]").textContent =
                `${index + 1}. ${labels.get(card.dataset.type)}`;
            card.querySelectorAll("[data-question-number]").forEach(number => {
                number.textContent = `${index + 1}.`;
            });
            actionButton(card, "up").disabled = index === 0;
            actionButton(card, "down").disabled = index === list.children.length - 1;
            actionButton(card, "duplicate").disabled = atLimit;
        });

        picker.hidden = atLimit;
        limit.hidden = !atLimit;
    }

    function createEntry(kind, value, index, canRemove) {
        const row = entryTemplate.content.firstElementChild.cloneNode(true);
        const input = row.querySelector("input");
        const remove = actionButton(row, "remove-entry");
        const label = `${kind === "option" ? "Option" : "Statement"} ${index + 1}`;
        input.value = value;
        input.dataset.entryKind = kind;
        input.setAttribute("aria-label", label);
        remove.setAttribute("aria-label", `Remove ${label.toLowerCase()}`);
        remove.disabled = !canRemove;
        return row;
    }

    function renderOptions(card, options, hasOther) {
        const rows = card.querySelector("[data-option-list]");
        rows.replaceChildren(...options.map((value, index) => createEntry("option", value, index, options.length > 2)));
        actionButton(card, "add-option").disabled = options.length >= maxEntries;
        if (card.dataset.type !== "Choice") return;

        actionButton(card, "add-other").hidden = hasOther;
        if (hasOther) {
            const row = entryTemplate.content.firstElementChild.cloneNode(true);
            row.dataset.otherOption = "";
            const input = row.querySelector("input");
            input.value = "Other";
            input.disabled = true;
            input.setAttribute("aria-label", "Other option preview");
            const remove = actionButton(row, "remove-entry");
            remove.dataset.action = "remove-other";
            remove.setAttribute("aria-label", "Remove Other option");
            rows.append(row);
        }
    }

    function renderLikert(card, options, statements) {
        const table = card.querySelector("[data-likert-table]");
        table.replaceChildren();
        const header = table.createTHead().insertRow();
        header.insertCell();
        options.forEach((value, index) => {
            const cell = document.createElement("th");
            cell.scope = "col";
            cell.append(createEntry("option", value, index, options.length > 2));
            header.append(cell);
        });

        const add = document.createElement("button");
        add.type = "button";
        add.className = "survey-add";
        add.dataset.action = "add-option";
        add.setAttribute("aria-label", "Add scale option");
        add.textContent = "+";
        add.disabled = options.length >= maxEntries;
        header.insertCell().append(add);

        const body = table.createTBody();
        statements.forEach((value, index) => {
            const row = body.insertRow();
            const cell = document.createElement("th");
            cell.scope = "row";
            cell.append(createEntry("statement", value, index, statements.length > 1));
            row.append(cell);
            options.forEach(() => {
                const marker = document.createElement("span");
                marker.className = "survey-radio-preview";
                marker.setAttribute("aria-hidden", "true");
                row.insertCell().append(marker);
            });
            row.insertCell();
        });
        actionButton(card, "add-statement").disabled = statements.length >= maxEntries;
    }

    function refreshPreviews(card) {
        if (card.dataset.type === "Text") {
            const longAnswer = field(card, "longAnswer").checked;
            card.querySelector("[data-short-answer]").hidden = longAnswer;
            card.querySelector("[data-long-answer]").hidden = !longAnswer;
        } else if (card.dataset.type === "Rating") {
            const preview = card.querySelector("[data-rating-preview]");
            const levels = Number(field(card, "ratingLevels").value);
            const symbol = field(card, "ratingSymbol").value;
            preview.dataset.symbol = symbol;
            preview.setAttribute("aria-label", `${levels}-level ${symbol.toLowerCase()} rating preview`);
            preview.replaceChildren(...Array.from({ length: levels }, (_, index) => {
                const marker = document.createElement("span");
                marker.setAttribute("aria-hidden", "true");
                marker.textContent = symbol === "Star" ? "☆" : index + 1;
                return marker;
            }));
        } else if (card.dataset.type === "Choice") {
            card.classList.toggle("survey-multiple", field(card, "multipleAnswers").checked);
        }
    }

    function appendQuestion(question) {
        const card = template.content.firstElementChild.cloneNode(true);
        const type = question.type;
        card.dataset.type = type;
        card.dataset.id = question.id ?? "00000000-0000-0000-0000-000000000000";

        card.querySelectorAll("[data-for-types]").forEach(group => {
            group.hidden = !group.dataset.forTypes.split(" ").includes(type);
        });

        const npsPrompt = "How likely are you to recommend us to a friend or colleague?";
        const prompt = field(card, "prompt");
        prompt.value = question.prompt ?? (type === "NetPromoterScore" ? npsPrompt : "");
        field(card, "ratingLevels").value = question.ratingLevels ?? 5;
        field(card, "ratingSymbol").value = question.ratingSymbol ?? "Star";
        field(card, "description").value = question.description ?? "";
        field(card, "lowScoreLabel").value = question.lowScoreLabel ?? "Not at all likely";
        field(card, "highScoreLabel").value = question.highScoreLabel ?? "Extremely likely";
        field(card, "required").checked = question.required ?? false;
        field(card, "multipleAnswers").checked = question.multipleAnswers ?? false;
        field(card, "longAnswer").checked = question.longAnswer ?? false;

        if (type === "Section") {
            card.querySelector("[data-prompt-label]").textContent = "Section title";
            prompt.placeholder = "Section title";
        } else if (type === "Choice" || type === "Ranking") {
            renderOptions(card, question.options ?? defaults("Option", type === "Choice" ? 2 : 3), question.hasOtherOption);
        } else if (type === "Likert") {
            renderLikert(card, question.options ?? defaults("Option", 5), question.statements ?? defaults("Statement", 2));
        }

        refreshPreviews(card);
        updateSummary(card);
        list.append(card);
        return card;
    }

    function readQuestion(card) {
        const { type, id } = card.dataset;
        return {
            id,
            type,
            prompt: field(card, "prompt").value,
            options: ["Choice", "Ranking", "Likert"].includes(type) ? entries(card, "option") : null,
            statements: type === "Likert" ? entries(card, "statement") : null,
            ratingLevels: type === "Rating" ? Number(field(card, "ratingLevels").value) : null,
            ratingSymbol: type === "Rating" ? field(card, "ratingSymbol").value : null,
            description: type === "Section" ? field(card, "description").value : null,
            required: field(card, "required").checked,
            multipleAnswers: field(card, "multipleAnswers").checked,
            longAnswer: field(card, "longAnswer").checked,
            hasOtherOption: card.querySelector("[data-other-option]") !== null,
            lowScoreLabel: type === "NetPromoterScore" ? field(card, "lowScoreLabel").value : null,
            highScoreLabel: type === "NetPromoterScore" ? field(card, "highScoreLabel").value : null
        };
    }

    function editEntries(card, button) {
        const options = entries(card, "option");
        const statements = entries(card, "statement");
        let hasOther = card.querySelector("[data-other-option]") !== null;
        let kind = "option";
        let index = 0;

        switch (button.dataset.action) {
            case "add-option":
                index = options.push("") - 1;
                break;
            case "add-statement":
                kind = "statement";
                index = statements.push("") - 1;
                break;
            case "add-other":
                hasOther = true;
                break;
            case "remove-other":
                hasOther = false;
                break;
            case "remove-entry": {
                const input = button.closest(".survey-entry").querySelector("input");
                kind = input.dataset.entryKind;
                const values = kind === "option" ? options : statements;
                index = entryInputs(card, kind).indexOf(input);
                values.splice(index, 1);
                index = Math.min(index, values.length - 1);
                break;
            }
        }

        if (card.dataset.type === "Likert") {
            renderLikert(card, options, statements);
        } else {
            renderOptions(card, options, hasOther);
        }

        const action = button.dataset.action;
        if (action === "add-other" || action === "remove-other") {
            actionButton(card, hasOther ? "remove-other" : "add-other").focus();
        } else {
            entryInputs(card, kind)[index]?.focus();
        }
    }

    let questions;
    try {
        questions = JSON.parse(payload.value);
    } catch {
        questions = null;
    }

    if (!Array.isArray(questions) || questions.some(question => !labels.has(question?.type))) {
        const error = document.createElement("p");
        error.className = "settings-validation";
        error.setAttribute("role", "alert");
        error.textContent = "The question editor could not load this draft. Open the survey again from the list.";
        builder.before(error);
        return;
    }

    questions.forEach(appendQuestion);
    refreshOrder();
    builder.hidden = false;
    submitButtons.forEach(button => { button.disabled = false; });
    if (missingQuestionModal?.dataset.openOnLoad === "true") {
        missingQuestionWarning?.show();
    }

    typeButtons.forEach(button => button.addEventListener("click", () => {
        if (list.children.length >= maxItems) return;

        const type = button.dataset.questionType;
        const card = appendQuestion({ type });
        picker.open = false;
        refreshOrder();
        selectQuestion(card);
        field(card, "prompt").focus();
        status.textContent = `${labels.get(type)} added.`;
    }));

    list.addEventListener("click", event => {
        const card = event.target.closest(".survey-question");
        if (!card) return;

        if (card !== activeQuestion) {
            selectQuestion(card);
            field(card, "prompt").focus();
            return;
        }

        const button = event.target.closest("button[data-action]");
        if (!button) return;

        const label = labels.get(card.dataset.type);
        const { action } = button.dataset;
        if (action === "duplicate") {
            const copy = appendQuestion({ ...readQuestion(card), id: undefined });
            card.after(copy);
            refreshOrder();
            selectQuestion(copy);
            field(copy, "prompt").focus();
            status.textContent = `${label} duplicated.`;
            return;
        }

        if (action === "remove") {
            const neighbor = card.nextElementSibling ?? card.previousElementSibling;
            selectQuestion(null);
            card.remove();
            refreshOrder();
            (neighbor ? neighbor.querySelector("[data-question-summary]") : picker.querySelector("summary")).focus();
            status.textContent = `${label} removed.`;
            return;
        }

        if (action === "up") {
            card.previousElementSibling.before(card);
        } else if (action === "down") {
            card.nextElementSibling.after(card);
        } else {
            editEntries(card, button);
            return;
        }
        refreshOrder();
        (button.disabled ? field(card, "prompt") : button).focus();
        status.textContent = `${label} moved to position ${[...list.children].indexOf(card) + 1}.`;
    });

    editor.addEventListener("click", event => {
        if (!event.target.closest(".survey-question") && !event.target.closest("[data-question-type]")) {
            selectQuestion(null);
        }
    });

    list.addEventListener("keydown", event => {
        if (event.key === "Escape" && activeQuestion?.contains(event.target)) {
            const summary = activeQuestion.querySelector("[data-question-summary]");
            selectQuestion(null);
            summary.focus();
        }
    });

    // Reveal the first invalid question before the browser focuses its input.
    list.addEventListener("invalid", event => {
        if (revealingInvalidQuestion) return;

        revealingInvalidQuestion = true;
        selectQuestion(event.target.closest(".survey-question"));
        queueMicrotask(() => { revealingInvalidQuestion = false; });
    }, true);

    list.addEventListener("change", event => refreshPreviews(event.target.closest("fieldset")));

    editor.addEventListener("submit", event => {
        if (submitting) {
            event.preventDefault();
            return;
        }

        if (missingQuestionWarning && event.submitter?.hasAttribute("data-requires-question") &&
            !list.querySelector(".survey-question:not([data-type='Section'])")) {
            event.preventDefault();
            missingQuestionWarning.show();
            return;
        }

        payload.value = JSON.stringify([...list.children].map(readQuestion));
        submitting = true;
    });

    window.addEventListener("pageshow", () => { submitting = false; });
})();
