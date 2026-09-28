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

    const colors = ["#4ac1c1", "#c2185b", "#f4b942", "#6c63ff", "#38a169"];
    let celebrationActive = false;

    const celebrate = () => {
        if (celebrationActive ||
            window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            return;
        }

        celebrationActive = true;
        const bounds = confettiTrigger.getBoundingClientRect();
        const originX = bounds.left + bounds.width / 2;
        const originY = bounds.top + bounds.height / 2;

        for (let index = 0; index < 28; index += 1) {
            const piece = document.createElement("span");
            const angle = (Math.PI * 2 * index) / 28;
            const distance = 65 + Math.random() * 105;
            piece.className = "notification-confetti-piece";
            piece.style.setProperty("--confetti-left", `${originX}px`);
            piece.style.setProperty("--confetti-top", `${originY}px`);
            piece.style.setProperty(
                "--confetti-x",
                `${Math.cos(angle) * distance}px`);
            piece.style.setProperty(
                "--confetti-rise",
                `${45 + Math.random() * 85}px`);
            piece.style.setProperty(
                "--confetti-fall",
                `${90 + Math.random() * 125}px`);
            const rotation = 360 + Math.random() * 540;
            piece.style.setProperty(
                "--confetti-half-rotation",
                `${rotation * 0.45}deg`);
            piece.style.setProperty(
                "--confetti-rotation",
                `${rotation}deg`);
            piece.style.setProperty(
                "--confetti-color",
                colors[index % colors.length]);
            modal.appendChild(piece);
            piece.addEventListener("animationend", () => piece.remove(),
                { once: true });
        }

        window.setTimeout(() => {
            celebrationActive = false;
        }, 1250);
    };

    confettiTrigger.addEventListener("pointerenter", celebrate);
    confettiTrigger.addEventListener("focus", celebrate);
    confettiTrigger.addEventListener("click", celebrate);
    window.requestAnimationFrame(celebrate);
})();
