(() => {
    const carousel = document.querySelector("[data-event-carousel]");
    if (!carousel) return;

    const cards = Array.from(carousel.querySelectorAll("[data-event-card]"));
    const dots = Array.from(carousel.querySelectorAll("[data-event-dot]"));
    const current = carousel.querySelector("[data-event-current]");
    let activeIndex = 0;

    carousel.querySelectorAll(".home-event-banner img").forEach((image) => {
        const setOrientation = () => {
            image.closest(".home-event-banner")?.classList.toggle(
                "is-portrait", image.naturalHeight > image.naturalWidth);
        };
        if (image.complete) setOrientation();
        else image.addEventListener("load", setOrientation, { once: true });
    });

    const show = (index) => {
        activeIndex = (index + cards.length) % cards.length;
        cards.forEach((card, cardIndex) => card.hidden = cardIndex !== activeIndex);
        dots.forEach((dot, dotIndex) => {
            dot.classList.toggle("is-active", dotIndex === activeIndex);
            dot.setAttribute("aria-current", dotIndex === activeIndex ? "true" : "false");
        });
        if (current) current.textContent = String(activeIndex + 1);
    };

    carousel.querySelector("[data-event-previous]")?.addEventListener("click", () => show(activeIndex - 1));
    carousel.querySelector("[data-event-next]")?.addEventListener("click", () => show(activeIndex + 1));
    dots.forEach((dot, index) => dot.addEventListener("click", () => show(index)));
})();
