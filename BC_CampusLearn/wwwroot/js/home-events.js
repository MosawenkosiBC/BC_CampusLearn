(() => {
    const carousel = document.querySelector("[data-event-carousel]");
    if (!carousel) return;

    const cards = Array.from(carousel.querySelectorAll("[data-event-card]"));
    const dots = Array.from(carousel.querySelectorAll("[data-event-dot]"));
    const current = carousel.querySelector("[data-event-current]");
    const previousButton = carousel.querySelector("[data-event-previous]");
    const nextButton = carousel.querySelector("[data-event-next]");
    const autoAdvanceDelay = 6000;
    let activeIndex = 0;
    let autoAdvanceTimer = null;

    carousel.querySelectorAll(".home-event-banner img").forEach((image) => {
        const setOrientation = () => {
            image.closest(".home-event-banner")?.classList.toggle(
                "is-portrait", image.naturalHeight > image.naturalWidth);
        };
        if (image.complete) setOrientation();
        else image.addEventListener("load", setOrientation, { once: true });
    });

    const show = (index) => {
        if (!cards.length) return;
        activeIndex = (index + cards.length) % cards.length;
        cards.forEach((card, cardIndex) => card.hidden = cardIndex !== activeIndex);
        dots.forEach((dot, dotIndex) => {
            dot.classList.toggle("is-active", dotIndex === activeIndex);
            dot.setAttribute("aria-current", dotIndex === activeIndex ? "true" : "false");
        });
        if (current) current.textContent = String(activeIndex + 1);
    };

    const stopAutoAdvance = () => {
        if (!autoAdvanceTimer) return;
        window.clearInterval(autoAdvanceTimer);
        autoAdvanceTimer = null;
    };

    const startAutoAdvance = () => {
        stopAutoAdvance();
        if (cards.length < 2 ||
            window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
        autoAdvanceTimer = window.setInterval(
            () => show(activeIndex + 1), autoAdvanceDelay);
    };

    previousButton?.addEventListener("click", () => {
        show(activeIndex - 1);
        startAutoAdvance();
    });
    nextButton?.addEventListener("click", () => {
        show(activeIndex + 1);
        startAutoAdvance();
    });
    dots.forEach((dot, index) => dot.addEventListener("click", () => {
        show(index);
        startAutoAdvance();
    }));

    carousel.addEventListener("mouseenter", stopAutoAdvance);
    carousel.addEventListener("mouseleave", startAutoAdvance);
    carousel.addEventListener("focusin", stopAutoAdvance);
    carousel.addEventListener("focusout", (event) => {
        if (!carousel.contains(event.relatedTarget)) startAutoAdvance();
    });
    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopAutoAdvance();
        else startAutoAdvance();
    });

    show(0);
    startAutoAdvance();
})();
