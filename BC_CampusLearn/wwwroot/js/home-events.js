(() => {
    const carousel = document.querySelector("[data-event-carousel]");
    if (!carousel) return;

    const cards = Array.from(carousel.querySelectorAll("[data-event-card]"));
    const dots = Array.from(carousel.querySelectorAll("[data-event-dot]"));
    const track = carousel.querySelector(".home-event-track");
    const current = carousel.querySelector("[data-event-current]");
    const previousButton = carousel.querySelector("[data-event-previous]");
    const nextButton = carousel.querySelector("[data-event-next]");
    const autoAdvanceDelay = 6000;
    let activeIndex = 0;
    let autoAdvanceTimer = null;
    let transitionToken = 0;
    let motionAnimations = [];

    carousel.querySelectorAll(".home-event-banner img").forEach((image) => {
        const setOrientation = () => {
            image.closest(".home-event-banner")?.classList.toggle(
                "is-portrait", image.naturalHeight > image.naturalWidth);
        };
        if (image.complete) setOrientation();
        else image.addEventListener("load", setOrientation, { once: true });
    });

    const updateControls = () => {
        dots.forEach((dot, dotIndex) => {
            const isActive = dotIndex === activeIndex;
            dot.classList.toggle("is-active", isActive);
            dot.setAttribute("aria-current", isActive ? "true" : "false");
        });
        if (current) current.textContent = String(activeIndex + 1);
    };

    const show = (index, direction = 1, animate = true) => {
        if (!cards.length) return;
        const targetIndex = (index + cards.length) % cards.length;
        const outgoingCard = cards[activeIndex];
        const incomingCard = cards[targetIndex];
        const reducedMotion = window.matchMedia(
            "(prefers-reduced-motion: reduce)").matches;

        if (targetIndex === activeIndex || !animate || reducedMotion ||
            typeof incomingCard.animate !== "function") {
            activeIndex = targetIndex;
            cards.forEach((card, cardIndex) => {
                card.hidden = cardIndex !== activeIndex;
                card.style.removeProperty("z-index");
            });
            updateControls();
            return;
        }

        transitionToken += 1;
        const currentTransition = transitionToken;
        motionAnimations.forEach((animation) => animation.cancel());
        motionAnimations = [];
        track?.classList.remove("is-transitioning");
        track?.style.removeProperty("height");
        cards.forEach((card) => {
            card.hidden = card !== outgoingCard;
            card.style.removeProperty("z-index");
        });

        const outgoingHeight = outgoingCard.getBoundingClientRect().height;
        if (track) track.style.height = `${outgoingHeight}px`;
        incomingCard.hidden = false;
        outgoingCard.hidden = false;
        outgoingCard.style.zIndex = "1";
        incomingCard.style.zIndex = "2";
        activeIndex = targetIndex;
        updateControls();

        const travel = direction >= 0 ? 1 : -1;
        const timing = {
            duration: 850,
            easing: "cubic-bezier(.22, 1, .36, 1)",
            fill: "both"
        };
        const animations = [
            outgoingCard.animate([
                { opacity: 1, transform: "translateX(0) scale(1)", filter: "blur(0)" },
                { opacity: 0, transform: `translateX(${-4 * travel}%) scale(.975)`, filter: "blur(5px)" }
            ], timing),
            incomingCard.animate([
                { opacity: 0, transform: `translateX(${6 * travel}%) scale(1.018)`, filter: "blur(7px)" },
                { opacity: 1, transform: "translateX(0) scale(1)", filter: "blur(0)" }
            ], timing)
        ];

        const incomingImage = incomingCard.querySelector(".home-event-banner img");
        const incomingInformation = incomingCard.querySelector(
            ".home-event-information");
        if (incomingImage) {
            animations.push(incomingImage.animate([
                { transform: `translateX(${1.8 * travel}%) scale(1.055)` },
                { transform: "translateX(0) scale(1)" }
            ], { ...timing, duration: 1050 }));
        }
        if (incomingInformation) {
            animations.push(incomingInformation.animate([
                { opacity: 0, transform: `translateX(${2.5 * travel}%) translateY(14px)` },
                { opacity: 1, transform: "translateX(0) translateY(0)" }
            ], { ...timing, delay: 90, duration: 760 }));
        }
        if (track) {
            const incomingHeight = incomingCard.getBoundingClientRect().height;
            track.classList.add("is-transitioning");
            animations.push(track.animate([
                { height: `${outgoingHeight}px` },
                { height: `${incomingHeight}px` }
            ], { ...timing, duration: 760 }));
        }
        motionAnimations = animations;

        Promise.allSettled(animations.map((animation) => animation.finished))
            .then(() => {
                if (currentTransition !== transitionToken) return;
                cards.forEach((card, cardIndex) => {
                    card.hidden = cardIndex !== activeIndex;
                    card.style.removeProperty("z-index");
                });
                track?.classList.remove("is-transitioning");
                track?.style.removeProperty("height");
                animations.forEach((animation) => animation.cancel());
                if (motionAnimations === animations) motionAnimations = [];
            });
    };

    const stopAutoAdvance = () => {
        carousel.classList.add("is-auto-paused");
        if (!autoAdvanceTimer) return;
        window.clearInterval(autoAdvanceTimer);
        autoAdvanceTimer = null;
    };

    const startAutoAdvance = () => {
        stopAutoAdvance();
        if (cards.length < 2 ||
            window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
        carousel.classList.remove("is-auto-paused");
        autoAdvanceTimer = window.setInterval(
            () => show(activeIndex + 1, 1), autoAdvanceDelay);
    };

    previousButton?.addEventListener("click", () => {
        show(activeIndex - 1, -1);
        startAutoAdvance();
    });
    nextButton?.addEventListener("click", () => {
        show(activeIndex + 1, 1);
        startAutoAdvance();
    });
    dots.forEach((dot, index) => dot.addEventListener("click", () => {
        show(index, index >= activeIndex ? 1 : -1);
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

    show(0, 1, false);
    startAutoAdvance();
})();
