(() => {
    const form = document.querySelector("[data-notification-selection-form]");
    const toggle = document.querySelector("[data-notification-selection-toggle]");
    if (!(form instanceof HTMLFormElement) ||
        !(toggle instanceof HTMLButtonElement)) {
        return;
    }

    const toolbar = form.querySelector("[data-notification-selection-toolbar]");
    const selectAllCheckbox = form.querySelector(
        "[data-notification-select-all]");
    const selectAllValue = form.querySelector(
        "[data-notification-select-all-value]");
    const selectionCount = form.querySelector(
        "[data-notification-selection-count]");
    const markReadButton = form.querySelector(
        "[data-notification-mark-read]");
    const deleteButton = form.querySelector("[data-notification-delete]");
    const deleteModal = document.querySelector(
        "[data-notification-delete-modal]");
    const deleteScope = deleteModal?.querySelector(
        "[data-notification-delete-scope]");
    const deleteConfirm = deleteModal?.querySelector(
        "[data-notification-delete-confirm]");
    const actionButtons = [...form.querySelectorAll(
        "[data-notification-selection-action]")];
    const checkboxes = [...form.querySelectorAll(
        "[data-notification-checkbox]")];
    const rows = [...form.querySelectorAll("[data-notification-row]")];
    const totalNotifications = Number.parseInt(
        form.dataset.totalNotifications ?? "0",
        10);
    const unreadNotifications = Number.parseInt(
        form.dataset.unreadNotifications ?? "0",
        10);
    let isSelectingAll = false;

    if (!(toolbar instanceof HTMLElement) ||
        !(selectAllCheckbox instanceof HTMLInputElement) ||
        !(selectAllValue instanceof HTMLInputElement) ||
        !(selectionCount instanceof HTMLElement) ||
        !(markReadButton instanceof HTMLButtonElement) ||
        !(deleteButton instanceof HTMLButtonElement) ||
        !(deleteModal instanceof HTMLDialogElement) ||
        !(deleteScope instanceof HTMLElement) ||
        !(deleteConfirm instanceof HTMLButtonElement)) {
        return;
    }

    let deleteSubmissionConfirmed = false;
    let longPressTimer = 0;
    let longPressStartX = 0;
    let longPressStartY = 0;
    let suppressCardClick = null;
    const mobileSelection = window.matchMedia("(max-width: 767.98px)");

    const updateSelection = () => {
        const selectedOnPage = checkboxes.filter(checkbox => checkbox.checked);
        const selectedCount = isSelectingAll
            ? totalNotifications
            : selectedOnPage.length;
        const hasSelection = selectedCount > 0;
        const hasUnreadSelection = isSelectingAll
            ? unreadNotifications > 0
            : checkboxes.some((checkbox, index) =>
                checkbox.checked && rows[index]?.classList.contains("is-unread"));

        selectAllCheckbox.checked = isSelectingAll;
        selectAllCheckbox.indeterminate = false;
        selectAllValue.value = isSelectingAll ? "true" : "false";
        selectionCount.textContent = `${selectedCount} selected`;
        markReadButton.hidden = !hasUnreadSelection;
        actionButtons.forEach(button => {
            button.disabled = !hasSelection;
        });
        rows.forEach((row, index) => {
            row.classList.toggle("is-selected", checkboxes[index]?.checked);
        });
    };

    const clearSelection = () => {
        isSelectingAll = false;
        checkboxes.forEach(checkbox => {
            checkbox.checked = false;
            checkbox.disabled = false;
        });
        updateSelection();
    };

    const setSelectionMode = enabled => {
        form.classList.toggle("is-selecting", enabled);
        toolbar.hidden = !enabled;
        toggle.textContent = enabled ? "Cancel" : "Select";
        toggle.classList.toggle("is-selection-active", enabled);
        toggle.setAttribute("aria-expanded", enabled ? "true" : "false");
        if (!enabled) {
            clearSelection();
        } else {
            selectAllCheckbox.focus();
        }
    };

    toggle.addEventListener("click", () => {
        setSelectionMode(!form.classList.contains("is-selecting"));
    });

    selectAllCheckbox.addEventListener("change", () => {
        isSelectingAll = selectAllCheckbox.checked;
        checkboxes.forEach(checkbox => {
            checkbox.disabled = isSelectingAll;
            checkbox.checked = isSelectingAll;
        });
        updateSelection();
    });

    checkboxes.forEach(checkbox => {
        checkbox.addEventListener("change", updateSelection);
    });

    rows.forEach((row, index) => {
        const card = row.querySelector(".notification-card");
        if (!(card instanceof HTMLAnchorElement)) {
            return;
        }

        card.addEventListener("click", event => {
            if (suppressCardClick === card) {
                event.preventDefault();
                suppressCardClick = null;
                return;
            }

            if (!form.classList.contains("is-selecting")) {
                return;
            }

            event.preventDefault();
            if (isSelectingAll) {
                return;
            }

            const checkbox = checkboxes[index];
            checkbox.checked = !checkbox.checked;
            updateSelection();
        });

        const cancelLongPress = () => {
            window.clearTimeout(longPressTimer);
            longPressTimer = 0;
        };

        card.addEventListener("pointerdown", event => {
            if (!mobileSelection.matches || event.pointerType === "mouse" ||
                form.classList.contains("is-selecting")) {
                return;
            }

            longPressStartX = event.clientX;
            longPressStartY = event.clientY;
            cancelLongPress();
            longPressTimer = window.setTimeout(() => {
                setSelectionMode(true);
                checkboxes[index].checked = true;
                suppressCardClick = card;
                updateSelection();
                longPressTimer = 0;
                window.setTimeout(() => {
                    if (suppressCardClick === card) {
                        suppressCardClick = null;
                    }
                }, 1000);
            }, 550);
        });

        card.addEventListener("pointermove", event => {
            if (Math.abs(event.clientX - longPressStartX) > 10 ||
                Math.abs(event.clientY - longPressStartY) > 10) {
                cancelLongPress();
            }
        });
        card.addEventListener("pointerup", cancelLongPress);
        card.addEventListener("pointercancel", cancelLongPress);
        card.addEventListener("contextmenu", event => {
            if (mobileSelection.matches) {
                event.preventDefault();
            }
        });
    });

    form.addEventListener("submit", event => {
        const submitter = event.submitter;
        if (!(submitter instanceof HTMLButtonElement) ||
            submitter.value !== "delete") {
            return;
        }

        if (deleteSubmissionConfirmed) {
            deleteSubmissionConfirmed = false;
            return;
        }

        event.preventDefault();
        const selectedCount = isSelectingAll
            ? totalNotifications
            : checkboxes.filter(checkbox => checkbox.checked).length;
        deleteScope.textContent = isSelectingAll
            ? "all of your notifications"
            : `${selectedCount} selected notification${selectedCount === 1 ? "" : "s"}`;
        deleteModal.showModal();
    });

    deleteModal.querySelectorAll("[data-notification-delete-cancel]")
        .forEach(button => button.addEventListener("click", () => {
            deleteModal.close();
        }));

    deleteModal.addEventListener("click", event => {
        if (event.target === deleteModal) {
            deleteModal.close();
        }
    });

    deleteConfirm.addEventListener("click", () => {
        deleteSubmissionConfirmed = true;
        deleteModal.close();
        form.requestSubmit(deleteButton);
    });

    updateSelection();
})();

(() => {
    const modal = document.querySelector("[data-notification-modal]");
    if (!(modal instanceof HTMLDialogElement)) {
        return;
    }

    modal.showModal();

    const closeModal = () => modal.close();
    modal.querySelectorAll("[data-notification-modal-close]")
        .forEach(button => button.addEventListener("click", closeModal));

    modal.addEventListener("close", () => {
        const url = new URL(window.location.href);
        url.searchParams.delete("notificationId");
        window.history.replaceState(
            {},
            "",
            `${url.pathname}${url.search}${url.hash}`);
    });

    modal.addEventListener("click", event => {
        if (event.target === modal) {
            closeModal();
        }
    });

    const confettiTrigger = modal.querySelector("[data-confetti-trigger]");
    if (!(confettiTrigger instanceof HTMLElement)) {
        return;
    }

    const colors = [
        "#c6005c", "#4ebdc2", "#f5b942", "#6f4a8a",
        "#2f80ed", "#7bc96f", "#ef6c57"
    ];
    const educationIcons = ["🎓", "📚", "✏️", "⭐", "🧠", "📝"];
    let celebrationActive = false;

    const celebrate = () => {
        if (celebrationActive ||
            window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            return;
        }

        celebrationActive = true;
        const celebration = document.createElement("div");
        celebration.className = "notification-confetti";
        celebration.setAttribute("aria-hidden", "true");

        for (let index = 0; index < 72; index += 1) {
            const piece = document.createElement("span");
            const isEducationIcon = index % 6 === 0;
            piece.className = isEducationIcon
                ? "notification-confetti-piece is-education-icon"
                : "notification-confetti-piece";
            if (isEducationIcon) {
                piece.textContent = educationIcons[
                    Math.floor(Math.random() * educationIcons.length)];
            }

            piece.style.setProperty("--confetti-x", `${Math.random() * 100}vw`);
            piece.style.setProperty(
                "--confetti-delay", `${-Math.random() * 0.9}s`);
            piece.style.setProperty(
                "--confetti-duration", `${3.4 + Math.random() * 2.2}s`);
            piece.style.setProperty(
                "--confetti-drift", `${-90 + Math.random() * 180}px`);
            piece.style.setProperty(
                "--confetti-rotation", `${360 + Math.random() * 720}deg`);
            piece.style.setProperty(
                "--confetti-size", `${8 + Math.random() * 8}px`);
            piece.style.setProperty(
                "--confetti-colour",
                colors[Math.floor(Math.random() * colors.length)]);
            celebration.append(piece);
        }

        modal.append(celebration);

        window.setTimeout(() => {
            celebration.remove();
            celebrationActive = false;
        }, 6500);
    };

    confettiTrigger.addEventListener("pointerenter", celebrate);
    confettiTrigger.addEventListener("focus", celebrate);
    confettiTrigger.addEventListener("click", celebrate);
    window.requestAnimationFrame(celebrate);
})();
