(() => {
    const form = document.querySelector("[data-comment-form]");
    if (form) {
        const text = form.querySelector("[data-comment-text]");
        const characterCount = form.querySelector("[data-comment-character-count]");
        const parentId = form.querySelector("[data-comment-parent-id]");
        const replying = form.querySelector("[data-comment-replying]");
        const replyingName = form.querySelector("[data-comment-replying-name]");
        const cancelReply = form.querySelector("[data-comment-reply-cancel]");
        const validation = form.querySelector("[data-comment-validation]");

        const clearValidation = () => {
            if (!validation) return;
            validation.textContent = "";
            validation.hidden = true;
        };

        const showValidation = (message) => {
            if (!validation) return;
            validation.textContent = message;
            validation.hidden = false;
        };

        const updateCharacterCount = () => {
            if (characterCount) characterCount.textContent = String(text?.value.length ?? 0);
            if (text) {
                text.style.height = "auto";
                text.style.height = `${Math.min(text.scrollHeight, 140)}px`;
            }
        };

        const clearReply = () => {
            if (parentId) parentId.value = "";
            if (replying) replying.hidden = true;
            if (replyingName) replyingName.textContent = "";
        };

        document.querySelectorAll("[data-comment-reply]").forEach((button) => {
            button.addEventListener("click", () => {
                if (parentId) parentId.value = button.dataset.commentId ?? "";
                if (replyingName) replyingName.textContent = button.dataset.authorName ?? "this comment";
                if (replying) replying.hidden = false;
                text?.focus();
                form.scrollIntoView({ behavior: "smooth", block: "nearest" });
            });
        });

        cancelReply?.addEventListener("click", () => {
            clearReply();
            text?.focus();
        });

        text?.addEventListener("input", () => {
            clearValidation();
            updateCharacterCount();
        });
        form.addEventListener("submit", (event) => {
            const value = text?.value.trim() ?? "";
            if (value && value.length <= 2000) return;

            event.preventDefault();
            showValidation(value
                ? "Comments cannot exceed 2000 characters."
                : "Enter a comment before posting.");
            text?.focus();
        });
        updateCharacterCount();
    }

    document.querySelectorAll("[data-comment-edit]").forEach((button) => {
        button.addEventListener("click", () => {
            const editForm = document.getElementById(button.dataset.editForm ?? "");
            const commentText = document.getElementById(button.dataset.editText ?? "");
            const actions = document.getElementById(button.dataset.editActions ?? "");
            if (!editForm) return;

            editForm.hidden = false;
            if (commentText) commentText.hidden = true;
            if (actions) actions.hidden = true;
            editForm.querySelector("textarea")?.focus();
        });
    });

    document.querySelectorAll("[data-comment-edit-form]").forEach((editForm) => {
        const textarea = editForm.querySelector("textarea");
        const editButton = document.querySelector(`[data-edit-form="${editForm.id}"]`);
        const commentText = document.getElementById(editButton?.dataset.editText ?? "");
        const actions = document.getElementById(editButton?.dataset.editActions ?? "");

        editForm.querySelector("[data-comment-edit-cancel]")?.addEventListener("click", () => {
            editForm.hidden = true;
            if (commentText) commentText.hidden = false;
            if (actions) actions.hidden = false;
        });
    });

    const deleteCommentId = document.querySelector("[data-comment-delete-id]");
    document.querySelectorAll("[data-comment-delete-trigger]").forEach((button) => {
        button.addEventListener("click", () => {
            if (deleteCommentId) {
                deleteCommentId.value = button.dataset.commentId ?? "";
            }
        });
    });

    const setRepliesExpanded = (button, replies, expanded) => {
        replies.classList.toggle("is-expanded", expanded);
        button.setAttribute("aria-expanded", String(expanded));
        const label = button.querySelector("span");
        const hiddenCount = button.dataset.hiddenCount ?? "0";
        if (label) label.textContent = expanded
            ? "Hide replies"
            : `View replies (${hiddenCount})`;
    };

    document.querySelectorAll("[data-comment-view-replies]").forEach((button) => {
        const replies = document.getElementById(button.dataset.repliesTarget ?? "");
        if (!replies) return;

        button.addEventListener("click", () => {
            setRepliesExpanded(
                button,
                replies,
                button.getAttribute("aria-expanded") !== "true");
        });
    });

    const targetId = window.location.hash.slice(1);
    const postedComment = targetId.startsWith("comment-")
        ? document.getElementById(targetId)
        : null;
    if (postedComment) {
        const replies = postedComment.closest("[data-comment-replies]");
        if (replies) {
            const button = document.querySelector(
                `[data-replies-target="${replies.id}"]`);
            if (button) setRepliesExpanded(button, replies, true);
        }

        window.setTimeout(() => {
            postedComment.scrollIntoView({ behavior: "smooth", block: "center" });
            postedComment.classList.add("is-new-comment");
            window.setTimeout(
                () => postedComment.classList.remove("is-new-comment"),
                1800);
        }, 80);
    }
})();
