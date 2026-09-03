(() => {
    document.querySelectorAll(".meeting-link-modal").forEach((modal) => {
        if (modal.parentElement !== document.body) {
            document.body.append(modal);
        }
    });

    const statusControl = document.querySelector(
        "[data-student-session-status-control]");
    if (statusControl) {
        const statusSelect = statusControl.querySelector(
            "[data-student-session-status-select]");
        const saveButton = statusControl.querySelector(
            "[data-student-session-status-save]");
        const cancelForm = statusControl.querySelector(
            "[data-student-session-cancel-form]");

        saveButton?.addEventListener("click", () => {
            if (statusSelect?.value !== "cancel") {
                return;
            }

            if (statusControl.dataset.cancellationReasonRequired === "true") {
                const modalElement = document.getElementById(
                    "student-cancel-session-modal");
                if (modalElement && window.bootstrap) {
                    bootstrap.Modal.getOrCreateInstance(modalElement).show();
                }
                return;
            }

            cancelForm?.requestSubmit();
        });
    }

    document.querySelectorAll("[data-session-information-toggle]")
        .forEach((toggle) => toggle.addEventListener("click", () => {
            const section = toggle.closest(".session-information-section");
            if (!section) return;
            const collapsed = section.classList.toggle("is-mobile-collapsed");
            toggle.setAttribute("aria-expanded", String(!collapsed));
            toggle.setAttribute("aria-label", collapsed
                ? "Show session information"
                : "Hide session information");
        }));

    const evaluationPanel = document.getElementById("review-session-modal");
    if (evaluationPanel?.parentElement !== document.body) {
        document.body.append(evaluationPanel);
    }
    const setEvaluationScrollLock = (locked) => {
        document.documentElement.classList.toggle(
            "tutor-evaluation-panel-open", locked);
        document.body.classList.toggle(
            "tutor-evaluation-panel-open", locked);
    };
    evaluationPanel?.addEventListener(
        "show.bs.modal", () => setEvaluationScrollLock(true));
    evaluationPanel?.addEventListener(
        "hidden.bs.modal", () => setEvaluationScrollLock(false));

    const evaluationForm = evaluationPanel?.querySelector(
        "[data-session-evaluation-form]");
    const evaluationQuestions = Array.from(
        evaluationForm?.querySelectorAll(".evaluation-question") ?? []);
    const clearError = (question) => {
        question.classList.remove("has-validation-error");
        question.querySelector(".evaluation-validation-error")?.remove();
        question.querySelectorAll("input, textarea")
            .forEach((control) => {
                control.removeAttribute("aria-invalid");
                control.removeAttribute("aria-describedby");
            });
    };
    const showEvaluationError = (question, message, index) => {
        clearError(question);
        const error = document.createElement("p");
        error.className = "evaluation-validation-error";
        error.id = `student-evaluation-error-${index}`;
        error.setAttribute("role", "alert");
        error.textContent = message;
        question.classList.add("has-validation-error");
        question.querySelectorAll("input, textarea")
            .forEach((control) => {
                control.setAttribute("aria-invalid", "true");
                control.setAttribute("aria-describedby", error.id);
            });
        question.append(error);
    };
    const validateQuestion = (question, index) => {
        const radio = question.querySelector("input[type='radio']");
        const text = question.querySelector("input:not([type='radio']), textarea");
        if (radio && !question.querySelector("input[type='radio']:checked")) {
            showEvaluationError(question, "Select one option.", index);
            return false;
        }
        const value = text?.value.trim() ?? "";
        if (text && !value) {
            showEvaluationError(question, "This response is required.", index);
            return false;
        }
        clearError(question);
        return true;
    };
    evaluationQuestions.forEach((question, index) => {
        question.querySelectorAll("input, textarea").forEach((control) => {
            control.addEventListener(
                control.type === "radio" ? "change" : "input",
                () => {
                    if (question.classList.contains("has-validation-error")) {
                        validateQuestion(question, index);
                    }
                });
        });
    });

    evaluationForm?.querySelectorAll(".evaluation-rating-group")
        .forEach((group) => {
            const options = Array.from(group.querySelectorAll(
                ".evaluation-rating-option"));
            const updateStars = () => {
                const selectedIndex = options.findIndex((option) =>
                    option.querySelector("input")?.checked);
                options.forEach((option, index) => option.classList.toggle(
                    "is-filled",
                    selectedIndex >= 0 && index <= selectedIndex));
            };
            group.querySelectorAll("input[type='radio']")
                .forEach((input) => input.addEventListener(
                    "change",
                    updateStars));
            updateStars();
        });

    evaluationForm?.addEventListener("submit", (event) => {
        const results = evaluationQuestions.map(validateQuestion);
        const firstInvalid = results.findIndex((valid) => !valid);
        if (firstInvalid < 0) return;
        event.preventDefault();
        const question = evaluationQuestions[firstInvalid];
        question.scrollIntoView({ behavior: "smooth", block: "center" });
        question.querySelector("input, textarea")?.focus({ preventScroll: true });
    });

    const chat = document.querySelector("[data-session-chat]");
    if (!chat) {
        return;
    }

    const bookingId = Number(chat.dataset.bookingId);
    const currentUserId = Number(chat.dataset.currentUserId);
    const messages = chat.querySelector("[data-session-chat-messages]");
    const form = chat.querySelector("[data-session-chat-form]");
    const input = chat.querySelector("[data-session-chat-input]");
    const error = chat.querySelector("[data-session-chat-error]");
    const typingIndicator = chat.querySelector(
        "[data-session-typing-indicator]");

    const showError = (message) => {
        error.textContent = message;
        error.hidden = !message;
    };

    const resizeInput = () => {
        if (!input) {
            return;
        }
        input.style.height = "auto";
        input.style.height = `${Math.min(input.scrollHeight, 140)}px`;
    };
    input?.addEventListener("input", resizeInput);
    resizeInput();

    const emojiToggle = form?.querySelector("[data-session-emoji-toggle]");
    const emojiPicker = form?.querySelector("[data-session-emoji-picker]");
    const closeEmojiPicker = () => {
        if (!emojiPicker || !emojiToggle) {
            return;
        }
        emojiPicker.hidden = true;
        emojiToggle.setAttribute("aria-expanded", "false");
    };

    emojiToggle?.addEventListener("click", () => {
        const willOpen = emojiPicker.hidden;
        emojiPicker.hidden = !willOpen;
        emojiToggle.setAttribute("aria-expanded", String(willOpen));
    });

    emojiPicker?.querySelectorAll("[data-session-emoji]")
        .forEach((button) => button.addEventListener("click", () => {
            const emoji = button.dataset.sessionEmoji || "";
            const start = input.selectionStart ?? input.value.length;
            const end = input.selectionEnd ?? start;
            const nextLength = input.value.length - (end - start) + emoji.length;
            if (nextLength > 2000) {
                return;
            }
            input.setRangeText(emoji, start, end, "end");
            input.dispatchEvent(new Event("input", { bubbles: true }));
            input.focus();
            closeEmojiPicker();
        }));

    document.addEventListener("click", (event) => {
        if (!form?.contains(event.target)) {
            closeEmojiPicker();
        }
    });
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeEmojiPicker();
        }
    });

    const getDateKey = (date) => [
        date.getFullYear(),
        String(date.getMonth() + 1).padStart(2, "0"),
        String(date.getDate()).padStart(2, "0")
    ].join("-");

    const getDateLabel = (date) => {
        const today = new Date();
        const yesterday = new Date(today);
        yesterday.setDate(today.getDate() - 1);
        if (getDateKey(date) === getDateKey(today)) {
            return "Today";
        }
        if (getDateKey(date) === getDateKey(yesterday)) {
            return "Yesterday";
        }
        const weekday = date.toLocaleDateString([], { weekday: "short" });
        const day = String(date.getDate()).padStart(2, "0");
        const month = date.toLocaleDateString([], { month: "short" });
        return `${weekday}, ${day} ${month}`;
    };

    const appendDateSeparator = (sentAt) => {
        const dateKey = getDateKey(sentAt);
        const datedMessages = messages.querySelectorAll(
            "[data-message-date]");
        const lastMessage = datedMessages[datedMessages.length - 1];
        if (lastMessage?.dataset.messageDate === dateKey) {
            return dateKey;
        }

        const separator = document.createElement("div");
        separator.className = "session-chat-date-separator";
        separator.dataset.messageDateSeparator = dateKey;
        const label = document.createElement("span");
        label.textContent = getDateLabel(sentAt);
        separator.append(label);
        messages.insertBefore(separator, typingIndicator || null);
        return dateKey;
    };

    if (!window.signalR) {
        form?.addEventListener("submit", (event) => event.preventDefault());
        const sendButton = form?.querySelector("button[type='submit']");
        if (sendButton) {
            sendButton.disabled = true;
        }
        showError("Chat could not load. Refresh the page and try again.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/session")
        .withAutomaticReconnect()
        .build();

    const appendMessage = (message) => {
        const sentAt = new Date(message.sentAt);
        const article = document.createElement("article");
        article.id = `message-${message.sessionMessageId}`;
        article.className = "session-chat-message";
        article.dataset.messageDate = appendDateSeparator(sentAt);
        const isMine = Number(message.senderBcUserId) === currentUserId;
        if (isMine) {
            article.classList.add("is-mine");
        }
        const body = document.createElement("p");
        body.textContent = message.messageText;
        const metadata = document.createElement("span");
        metadata.className = "session-chat-message-meta";
        const time = document.createElement("time");
        time.dateTime = sentAt.toISOString();
        time.textContent = sentAt.toLocaleTimeString([], {
            hour: "2-digit",
            minute: "2-digit"
        });
        metadata.append(time);
        if (isMine) {
            const receipt = document.createElement("span");
            receipt.className = "session-message-read-receipt";
            receipt.dataset.messageReadReceipt = "";
            receipt.setAttribute("aria-label", "Sent");
            receipt.title = "Sent";
            receipt.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 24 24" aria-hidden="true"><path d="M0 0h24v24H0z" fill="none"/><path fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M2.5 13.833L6 17.5l1.024-1.073M16.5 6.5l-6.063 6.352m-2.937.981L11 17.5l10.5-11"/></svg>';
            metadata.append(receipt);
        }
        article.append(body, metadata);
        messages.insertBefore(article, typingIndicator || null);
        messages.scrollTop = messages.scrollHeight;
    };

    let typingTimeout;
    let typingWasSent = false;
    const sendTypingState = async (isTyping) => {
        if (connection.state !== "Connected" || typingWasSent === isTyping) {
            return;
        }
        typingWasSent = isTyping;
        try {
            await connection.invoke("SetTyping", bookingId, isTyping);
        } catch {
            typingWasSent = false;
        }
    };

    input?.addEventListener("input", () => {
        window.clearTimeout(typingTimeout);
        const hasText = input.value.trim().length > 0;
        void sendTypingState(hasText);
        if (hasText) {
            typingTimeout = window.setTimeout(
                () => void sendTypingState(false),
                1400);
        }
    });

    connection.on("TypingChanged", (typingUpdate) => {
        if (Number(typingUpdate.bookingId) !== bookingId ||
            Number(typingUpdate.userBcUserId) === currentUserId) {
            return;
        }
        typingIndicator.hidden = !typingUpdate.isTyping;
        if (typingUpdate.isTyping) {
            messages.scrollTop = messages.scrollHeight;
        }
    });

    connection.on("MessagesRead", (receiptUpdate) => {
        if (Number(receiptUpdate.bookingId) !== bookingId) {
            return;
        }
        receiptUpdate.messageIds.forEach((messageId) => {
            const receipt = document
                .getElementById(`message-${messageId}`)
                ?.querySelector("[data-message-read-receipt]");
            if (receipt) {
                receipt.classList.add("is-read");
                receipt.setAttribute("aria-label", "Read");
                receipt.title = "Read";
            }
        });
    });

    connection.on("ReceiveMessage", async (message) => {
        if (Number(message.senderBcUserId) !== currentUserId) {
            typingIndicator.hidden = true;
        }
        appendMessage(message);
        if (Number(message.senderBcUserId) !== currentUserId) {
            try {
                await connection.invoke("MarkMessagesRead", bookingId);
            } catch {
                // The next page load will retry marking visible messages read.
            }
        }
    });
    connection.onreconnected(() => connection.invoke(
        "JoinSession",
        bookingId));

    const startConnection = async () => {
        try {
            await connection.start();
            await connection.invoke("JoinSession", bookingId);
            await connection.invoke("MarkMessagesRead", bookingId);
            showError("");
        } catch {
            showError("Chat could not connect. Retrying...");
            window.setTimeout(startConnection, 3000);
        }
    };

    form?.addEventListener("submit", async (event) => {
        event.preventDefault();
        const text = input.value.trim();
        if (!text) {
            return;
        }

        try {
            await connection.invoke("SendMessage", bookingId, text);
            await sendTypingState(false);
            input.value = "";
            resizeInput();
            showError("");
        } catch (exception) {
            showError(exception.message || "The message could not be sent.");
        }
    });

    messages.scrollTop = messages.scrollHeight;
    startConnection();
})();
