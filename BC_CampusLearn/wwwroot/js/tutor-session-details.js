(() => {
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
            const seconds = totalSeconds % 60;
            startRemaining.textContent = hours > 0
                ? `${hours}h ${minutes}m ${seconds}s Left`
                : `${minutes}m ${seconds}s Left`;
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
    if (!chat || !window.signalR) {
        return;
    }

    const bookingId = Number(chat.dataset.bookingId);
    const currentUserId = Number(chat.dataset.currentUserId);
    const messages = chat.querySelector("[data-session-chat-messages]");
    const form = chat.querySelector("[data-session-chat-form]");
    const input = chat.querySelector("[data-session-chat-input]");
    const error = chat.querySelector("[data-session-chat-error]");
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/session")
        .withAutomaticReconnect()
        .build();

    const showError = (message) => {
        error.textContent = message;
        error.hidden = !message;
    };

    const appendMessage = (message) => {
        const article = document.createElement("article");
        article.className = "session-chat-message";
        if (Number(message.senderBcUserId) === currentUserId) {
            article.classList.add("is-mine");
        }
        const sender = document.createElement("strong");
        sender.textContent = message.senderName;
        const body = document.createElement("p");
        body.textContent = message.messageText;
        const time = document.createElement("time");
        const sentAt = new Date(message.sentAt);
        time.dateTime = sentAt.toISOString();
        time.textContent = sentAt.toLocaleTimeString([], {
            hour: "2-digit",
            minute: "2-digit"
        });
        article.append(sender, body, time);
        messages.append(article);
        messages.scrollTop = messages.scrollHeight;
    };

    connection.on("ReceiveMessage", appendMessage);
    connection.onreconnected(() => connection.invoke(
        "JoinSession",
        bookingId));

    const startConnection = async () => {
        try {
            await connection.start();
            await connection.invoke("JoinSession", bookingId);
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
            input.value = "";
            showError("");
        } catch (exception) {
            showError(exception.message || "The message could not be sent.");
        }
    });

    messages.scrollTop = messages.scrollHeight;
    startConnection();
})();
