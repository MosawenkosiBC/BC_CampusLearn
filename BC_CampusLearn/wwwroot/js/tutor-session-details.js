(() => {
    document.querySelectorAll(".meeting-link-modal").forEach((modal) => {
        if (modal.parentElement !== document.body) {
            document.body.append(modal);
        }
    });

    document.querySelectorAll("[data-session-information-toggle]")
        .forEach((toggle) => {
            toggle.addEventListener("click", () => {
                const section = toggle.closest(".session-information-section");
                if (!section) {
                    return;
                }

                const isCollapsed = section.classList.toggle(
                    "is-mobile-collapsed");
                toggle.setAttribute("aria-expanded", String(!isCollapsed));
                toggle.setAttribute(
                    "aria-label",
                    isCollapsed
                        ? "Show session information"
                        : "Hide session information");
            });
        });

    const evaluationPanel = document.getElementById("review-session-modal");
    const setEvaluationScrollLock = (isLocked) => {
        document.documentElement.classList.toggle(
            "tutor-evaluation-panel-open",
            isLocked);
        document.body.classList.toggle(
            "tutor-evaluation-panel-open",
            isLocked);
    };
    evaluationPanel?.addEventListener(
        "show.bs.modal",
        () => setEvaluationScrollLock(true));
    evaluationPanel?.addEventListener(
        "hidden.bs.modal",
        () => setEvaluationScrollLock(false));

    const evaluationForm = evaluationPanel?.querySelector(
        "[data-tutor-evaluation-form]");
    const evaluationQuestions = Array.from(
        evaluationForm?.querySelectorAll(".evaluation-question") ?? []);

    const clearEvaluationError = (question) => {
        question.classList.remove("has-validation-error");
        question.querySelector(".evaluation-validation-error")?.remove();
        question.querySelectorAll("input, textarea, select")
            .forEach((control) => {
                control.removeAttribute("aria-invalid");
                control.removeAttribute("aria-describedby");
            });
    };

    const showEvaluationError = (question, message, index) => {
        clearEvaluationError(question);
        const error = document.createElement("p");
        error.className = "evaluation-validation-error";
        error.id = `evaluation-error-${index}`;
        error.setAttribute("role", "alert");
        error.textContent = message;
        question.classList.add("has-validation-error");
        question.querySelectorAll("input, textarea, select")
            .forEach((control) => {
                control.setAttribute("aria-invalid", "true");
                control.setAttribute("aria-describedby", error.id);
            });
        question.append(error);
    };

    const isHttpUrl = (value) => {
        try {
            const url = new URL(value);
            return url.protocol === "http:" || url.protocol === "https:";
        } catch {
            return false;
        }
    };

    const validateEvaluationQuestion = (question, index) => {
        const radio = question.querySelector("input[type='radio']");
        const control = question.querySelector(
            "input:not([type='radio']), textarea, select");
        const isRequired = question.hasAttribute("data-evaluation-required");

        if (radio && isRequired &&
            !question.querySelector("input[type='radio']:checked")) {
            showEvaluationError(question, "Select Yes or No.", index);
            return false;
        }

        const value = control?.value.trim() ?? "";
        if (control && isRequired && !value) {
            showEvaluationError(question, "This response is required.", index);
            return false;
        }

        if (control?.maxLength > 0 && value.length > control.maxLength) {
            showEvaluationError(
                question,
                `Use no more than ${control.maxLength} characters.`,
                index);
            return false;
        }

        if (value && question.hasAttribute("data-evaluation-url") &&
            !isHttpUrl(value)) {
            showEvaluationError(
                question,
                "Enter a valid HTTP or HTTPS recording link.",
                index);
            return false;
        }

        clearEvaluationError(question);
        return true;
    };

    evaluationQuestions.forEach((question, index) => {
        question.querySelectorAll("input, textarea, select")
            .forEach((control) => {
                const eventName = control.type === "radio"
                    ? "change"
                    : "input";
                control.addEventListener(eventName, () => {
                    if (question.classList.contains("has-validation-error")) {
                        validateEvaluationQuestion(question, index);
                    }
                });
            });
    });

    evaluationForm?.addEventListener("submit", (event) => {
        const results = evaluationQuestions.map((question, index) =>
            validateEvaluationQuestion(question, index));
        const firstInvalidIndex = results.findIndex((isValid) => !isValid);
        if (firstInvalidIndex < 0) {
            return;
        }

        event.preventDefault();
        const firstInvalidQuestion = evaluationQuestions[firstInvalidIndex];
        firstInvalidQuestion.scrollIntoView({
            behavior: "smooth",
            block: "center"
        });
        firstInvalidQuestion.querySelector("input, textarea, select")?.focus({
            preventScroll: true
        });
    });

    const runtime = document.querySelector("[data-session-runtime]");
    const runtimeOutput = document.querySelector(
        "[data-session-runtime-countdown]");
    const hoursOutput = runtimeOutput?.querySelector(
        "[data-session-hours]");
    const minutesOutput = runtimeOutput?.querySelector(
        "[data-session-minutes]");
    const secondsOutput = runtimeOutput?.querySelector(
        "[data-session-seconds]");
    const countdownDurationSeconds = Number(
        runtime?.dataset.countdownDurationSeconds || 3959);

    const renderRingProgress = (totalSeconds) => {
        if (!hoursOutput || !minutesOutput || !secondsOutput) {
            return;
        }

        const boundedSeconds = Math.max(
            0,
            Math.min(totalSeconds, countdownDurationSeconds));
        const seconds = boundedSeconds % 60;
        const secondsWithinHour = boundedSeconds % 3600;
        const initialPartialHour = countdownDurationSeconds % 3600;
        const initialHours = Math.floor(countdownDurationSeconds / 3600);
        const currentHours = Math.floor(boundedSeconds / 3600);
        const minuteCycleLength = currentHours === initialHours
            ? Math.max(initialPartialHour, 1)
            : 3600;
        const ringValues = [
            initialHours > 0
                ? currentHours / initialHours * 100
                : 0,
            secondsWithinHour / minuteCycleLength * 100,
            seconds / 59 * 100
        ];
        [hoursOutput, minutesOutput, secondsOutput]
            .forEach((output, index) => {
                output.parentElement?.style.setProperty(
                    "--ring-progress",
                    `${ringValues[index]}%`);
            });
    };

    if (runtime &&
        runtimeOutput &&
        runtime.dataset.countdownMode === "session-completion") {
        const countdownTarget = new Date(runtime.dataset.countdownTarget);
        let completionReloaded = false;

        const renderCountdown = () => {
            const remaining = Math.max(
                0,
                countdownTarget.getTime() - Date.now());
            const totalSeconds = Math.ceil(remaining / 1000);
            const hours = Math.floor(totalSeconds / 3600);
            const minutes = Math.floor((totalSeconds % 3600) / 60);
            const seconds = totalSeconds % 60;
            const parts = [hours, minutes, seconds]
                .map((value) => String(value).padStart(2, "0"));
            renderRingProgress(totalSeconds);
            if (hoursOutput && minutesOutput && secondsOutput) {
                hoursOutput.textContent = parts[0];
                minutesOutput.textContent = parts[1];
                secondsOutput.textContent = parts[2];
            } else {
                runtimeOutput.textContent = parts.join(":");
            }

            if (remaining === 0 &&
                !completionReloaded) {
                completionReloaded = true;
                window.setTimeout(() => window.location.reload(), 1500);
            }
        };

        renderCountdown();
        window.setInterval(renderCountdown, 1000);
    } else if (runtime && runtimeOutput) {
        const isCompleted = runtime.dataset.countdownMode === "completed";
        const displayedSeconds = isCompleted ? 0 : countdownDurationSeconds;
        renderRingProgress(displayedSeconds);
        if (isCompleted && hoursOutput && minutesOutput && secondsOutput) {
            hoursOutput.textContent = "00";
            minutesOutput.textContent = "00";
            secondsOutput.textContent = "00";
        }
    }

    const startRemaining = document.querySelector(
        "[data-session-start-remaining]");
    if (startRemaining) {
        const scheduledStart = new Date(startRemaining.dataset.sessionStart);
        const sessionIsActive = startRemaining.dataset.sessionActive === "true";
        const sessionIsLocked = startRemaining.dataset.sessionLocked === "true";
        const startTriggers = document.querySelectorAll(
            "[data-session-start-trigger]");

        const renderStartRemaining = () => {
            const remainingMilliseconds =
                scheduledStart.getTime() - Date.now();
            const startWindowIsOpen =
                remainingMilliseconds <= 5 * 60 * 1000 &&
                remainingMilliseconds > -15 * 60 * 1000;
            startTriggers.forEach((trigger) => {
                trigger.disabled = !startWindowIsOpen;
            });

            if (remainingMilliseconds <= 0) {
                startRemaining.textContent = "Starts now";
                return;
            }

            const totalSeconds = Math.ceil(remainingMilliseconds / 1000);
            const hours = Math.floor(totalSeconds / 3600);
            const minutes = Math.floor((totalSeconds % 3600) / 60);
            startRemaining.textContent = hours > 0
                ? `${hours}h ${minutes}m Left`
                : `${Math.max(1, minutes)}m Left`;
        };

        if (sessionIsLocked) {
            startRemaining.textContent = "Session completed";
            startTriggers.forEach((trigger) => {
                trigger.disabled = true;
            });
        } else if (sessionIsActive) {
            startRemaining.textContent = "Session active";
        } else {
            renderStartRemaining();
            window.setInterval(renderStartRemaining, 1000);
        }
    }

    const statusControl = document.querySelector(
        "[data-session-status-control]");
    if (statusControl) {
        const statusSelect = statusControl.querySelector(
            "[data-session-status-select]");
        const saveButton = statusControl.querySelector(
            "[data-session-status-save]");
        const startForm = statusControl.querySelector(
            "[data-session-start-form]");
        const declineForm = statusControl.querySelector(
            "[data-session-decline-form]");

        saveButton?.addEventListener("click", () => {
            const action = statusSelect?.value;
            const modalId = action === "confirm"
                ? "meeting-link-modal"
                : action === "cancel"
                    ? "decline-session-modal"
                    : null;

            if (modalId) {
                const modalElement = document.getElementById(modalId);
                if (modalElement && window.bootstrap) {
                    bootstrap.Modal.getOrCreateInstance(modalElement).show();
                }
                return;
            }

            if (action === "start") {
                startForm?.requestSubmit();
            } else if (action === "decline") {
                declineForm?.requestSubmit();
            }
        });
    }

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

    form.addEventListener("submit", async (event) => {
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
