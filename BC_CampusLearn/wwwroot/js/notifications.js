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
