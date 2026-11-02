(() => {
    const form = document.querySelector("[data-announcement-form]");
    if (!form) return;
    const content = form.querySelector("[data-announcement-content]");
    const host = form.querySelector("[data-announcement-editor]");
    const error = form.querySelector("[data-announcement-validation]");
    const audienceHelp = form.querySelector("[data-announcement-audience-help]");
    const progress = form.querySelector("[data-announcement-progress]");
    const submitButton = form.querySelector('button[type="submit"]');
    let sending = false;
    window.addEventListener("pageshow", () => {
        sending = false;
        form.removeAttribute("aria-busy");
        submitButton.disabled = false;
        submitButton.textContent = "Send announcement";
        progress.hidden = true;
    });
    const updateAudienceHelp = () => {
        const selected = form.querySelector('input[name="Input.Audience"]:checked');
        audienceHelp.textContent = selected?.value === "TutorsOnly"
            ? "Notifications will be sent to tutors only."
            : "Notifications will be sent to all students, including tutors.";
    };
    form.querySelectorAll('input[name="Input.Audience"]').forEach((input) => {
        input.addEventListener("change", updateAudienceHelp);
    });
    updateAudienceHelp();
    let editor = null;
    if (window.Quill && window.createResourceRichTextEditor) {
        content.hidden = true;
        host.hidden = false;
        editor = window.createResourceRichTextEditor(host, "Write your announcement here...");
        editor.root.setAttribute("aria-label", "Announcement message");
        editor.root.setAttribute("aria-multiline", "true");
        if (content.value.trim()) {
            try { editor.setContents(JSON.parse(content.value), "silent"); }
            catch { editor.setText(content.value, "silent"); }
        }
        editor.on("text-change", () => {
            content.value = JSON.stringify(editor.getContents());
            error.textContent = "";
        });
    }
    form.addEventListener("submit", (event) => {
        if (sending) {
            event.preventDefault();
            return;
        }
        if (editor && !editor.getText().trim()) {
            event.preventDefault();
            error.textContent = "Enter an announcement message.";
            editor.focus();
            return;
        }
        if (editor) content.value = JSON.stringify(editor.getContents());
        if (!event.defaultPrevented && form.checkValidity()) {
            sending = true;
            form.setAttribute("aria-busy", "true");
            submitButton.disabled = true;
            submitButton.textContent = "Sending…";
            progress.hidden = false;
        }
    });
})();
