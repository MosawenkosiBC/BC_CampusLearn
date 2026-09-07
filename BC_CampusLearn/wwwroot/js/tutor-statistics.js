(() => {
    "use strict";

    const periodForm = document.querySelector("[data-statistics-period-form]");

    if (periodForm) {
        const periodSelect = periodForm.querySelector("[data-statistics-period]");
        const editDatesButton = periodForm.querySelector("[data-statistics-edit-dates]");
        const customDateModal = document.querySelector("#statistics-custom-date-modal");
        const currentPeriod = periodForm.dataset.currentPeriod;

        const openCustomDateModal = () => {
            if (customDateModal && window.bootstrap) {
                window.bootstrap.Modal.getOrCreateInstance(customDateModal).show();
            }
        };

        periodSelect.addEventListener("change", () => {
            if (periodSelect.value === "custom") {
                openCustomDateModal();
                return;
            }

            periodForm.requestSubmit();
        });

        editDatesButton?.addEventListener("click", openCustomDateModal);

        customDateModal?.addEventListener("hidden.bs.modal", () => {
            periodSelect.value = currentPeriod;
        });
    }

    const statisticsCarousel = document.querySelector(
        "[data-statistics-carousel]");
    if (statisticsCarousel) {
        const track = statisticsCarousel.querySelector(
            "[data-statistics-carousel-track]");
        const cards = Array.from(
            track?.querySelectorAll(".tutor-stat-card-premium") ?? []);
        const previousButton = statisticsCarousel.querySelector(
            "[data-statistics-carousel-previous]");
        const nextButton = statisticsCarousel.querySelector(
            "[data-statistics-carousel-next]");
        const currentOutput = statisticsCarousel.querySelector(
            "[data-statistics-carousel-current]");
        const totalOutput = statisticsCarousel.querySelector(
            "[data-statistics-carousel-total]");
        const mobileQuery = window.matchMedia("(max-width: 767.98px)");
        const autoplayDuration = 5000;
        let currentIndex = 0;
        let scrollFrame;
        let previousTimestamp;
        let autoplayElapsed = 0;

        if (totalOutput) {
            totalOutput.textContent = String(cards.length);
        }

        const updateCarousel = (nextIndex) => {
            currentIndex = Math.max(0, Math.min(nextIndex, cards.length - 1));
            if (currentOutput) {
                currentOutput.textContent = String(currentIndex + 1);
            }
            if (previousButton) {
                previousButton.disabled = currentIndex === 0;
            }
            if (nextButton) {
                nextButton.disabled = currentIndex === cards.length - 1;
            }
        };

        const resetAutoplay = () => {
            autoplayElapsed = 0;
            previousTimestamp = undefined;
        };

        const showCard = (index, resetTimer = true) => {
            const card = cards[index];
            if (!track || !card) {
                return;
            }
            track.scrollTo({ left: card.offsetLeft, behavior: "smooth" });
            updateCarousel(index);
            if (resetTimer) {
                resetAutoplay();
            }
        };

        previousButton?.addEventListener(
            "click", () => showCard(currentIndex - 1));
        nextButton?.addEventListener(
            "click", () => showCard(currentIndex + 1));

        track?.addEventListener("scroll", () => {
            window.cancelAnimationFrame(scrollFrame);
            scrollFrame = window.requestAnimationFrame(() => {
                const nextIndex = cards.reduce((closest, card, index) =>
                    Math.abs(card.offsetLeft - track.scrollLeft) <
                    Math.abs(cards[closest].offsetLeft - track.scrollLeft)
                        ? index
                        : closest, 0);
                if (nextIndex !== currentIndex) {
                    resetAutoplay();
                }
                updateCarousel(nextIndex);
            });
        }, { passive: true });

        const runAutoplay = (timestamp) => {
            const canPlay = mobileQuery.matches && !document.hidden &&
                cards.length > 1;
            if (canPlay) {
                if (previousTimestamp !== undefined) {
                    autoplayElapsed += timestamp - previousTimestamp;
                }
                if (autoplayElapsed >= autoplayDuration) {
                    autoplayElapsed = 0;
                    showCard((currentIndex + 1) % cards.length, false);
                }
            }
            previousTimestamp = timestamp;
            window.requestAnimationFrame(runAutoplay);
        };

        document.addEventListener("visibilitychange", () => {
            previousTimestamp = undefined;
        });
        mobileQuery.addEventListener("change", resetAutoplay);

        updateCarousel(0);
        window.requestAnimationFrame(runAutoplay);
    }

    if (typeof window.Chart === "undefined") {
        return;
    }

    const parseValues = (element, name) => {
        try {
            return JSON.parse(element.dataset[name] || "[]");
        } catch {
            return [];
        }
    };

    const moduleCanvas = document.querySelector("[data-tutor-module-chart]");

    if (moduleCanvas) {
        const labels = parseValues(moduleCanvas, "labels");
        const completedValues = parseValues(moduleCanvas, "completedValues");
        const availabilityColors = [
            "#ad0151",
            "#4ac1c1",
            "#713b72",
            "#35658a",
            "#6f6f6f"
        ];

        new window.Chart(moduleCanvas, {
            type: "bar",
            data: {
                labels,
                datasets: [
                    {
                        label: "Sessions completed",
                        data: completedValues,
                        backgroundColor: availabilityColors.slice(0, labels.length),
                        borderWidth: 0,
                        borderRadius: 4,
                        categoryPercentage: 0.9,
                        barPercentage: 0.92,
                        maxBarThickness: 64
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    intersect: false,
                    mode: "index"
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: (context) => {
                                const count = context.parsed.y;
                                return `${count} ${count === 1 ? "session" : "sessions"} completed`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        title: {
                            display: true,
                            text: "Module",
                            color: "#535a65",
                            font: { size: 12, weight: "600" }
                        },
                        ticks: {
                            color: "#858b94",
                            font: { size: 12 },
                            maxRotation: 0
                        },
                        border: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        title: {
                            display: true,
                            text: "Number of sessions completed",
                            color: "#535a65",
                            font: { size: 12, weight: "600" }
                        },
                        ticks: {
                            color: "#858b94",
                            font: { size: 12 },
                            precision: 0,
                            stepSize: 1
                        },
                        grid: { color: "rgba(35, 40, 48, 0.06)" },
                        border: { display: false }
                    }
                }
            }
        });
    }

})();
