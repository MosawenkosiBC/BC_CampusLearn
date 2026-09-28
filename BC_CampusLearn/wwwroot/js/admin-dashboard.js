(() => {
    "use strict";

    const filter = document.querySelector("[data-session-activity-filter]");
    const filterTrigger = filter?.querySelector(
        "[data-session-activity-filter-trigger]");
    const filterLabel = filter?.querySelector(
        "[data-session-activity-filter-label]");
    const filterMenu = filter?.querySelector(
        "[data-session-activity-filter-menu]");
    const filterOptions = filter?.querySelectorAll(
        "[data-session-activity-option]") ?? [];
    const views = document.querySelectorAll("[data-session-activity-view]");
    const charts = new WeakMap();
    const revealDuration = 1050;
    let selectedPeriod = "weekly";

    if (!filter || !filterTrigger || !filterLabel || !filterMenu ||
        filterOptions.length === 0 || views.length === 0) {
        return;
    }

    const setFilterOpen = isOpen => {
        filter.classList.toggle("is-open", isOpen);
        filterTrigger.setAttribute("aria-expanded", String(isOpen));
        filterMenu.hidden = !isOpen;
    };

    const revealPlugin = {
        id: "sessionActivityReveal",
        beforeInit(chart) {
            chart.$revealProgress = 0;
        },
        beforeDatasetsDraw(chart) {
            const { ctx, chartArea } = chart;
            const progress = chart.$revealProgress ?? 1;
            ctx.save();
            ctx.beginPath();
            ctx.rect(
                chartArea.left - 8,
                chartArea.top - 8,
                (chartArea.width + 16) * progress,
                chartArea.height + 16);
            ctx.clip();
        },
        afterDatasetsDraw(chart) {
            chart.ctx.restore();
        }
    };

    const tooltipCardPlugin = {
        id: "sessionActivityTooltipCard",
        beforeTooltipDraw(chart) {
            const tooltip = chart.tooltip;
            if (!tooltip || tooltip.opacity === 0) {
                return;
            }

            const { ctx } = chart;
            ctx.save();
            ctx.fillStyle = "#ffffff";
            ctx.shadowColor = "rgba(25, 30, 38, 0.18)";
            ctx.shadowBlur = 18;
            ctx.shadowOffsetY = 7;
            ctx.beginPath();
            ctx.roundRect(
                tooltip.x,
                tooltip.y,
                tooltip.width,
                tooltip.height,
                9);
            ctx.fill();
            ctx.restore();
        }
    };

    const easeInOutCubic = progress => progress < 0.5
        ? 4 * progress * progress * progress
        : 1 - Math.pow(-2 * progress + 2, 3) / 2;

    const animateChartReveal = chart => {
        if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            chart.$revealProgress = 1;
            chart.draw();
            return;
        }

        const startedAt = window.performance.now();
        const drawFrame = now => {
            const elapsed = Math.min(
                (now - startedAt) / revealDuration,
                1);
            chart.$revealProgress = easeInOutCubic(elapsed);
            chart.draw();

            if (elapsed < 1) {
                chart.$revealFrame = window.requestAnimationFrame(drawFrame);
            } else {
                chart.$revealFrame = null;
            }
        };

        chart.$revealFrame = window.requestAnimationFrame(drawFrame);
    };

    const destroyChart = chart => {
        if (chart.$revealFrame) {
            window.cancelAnimationFrame(chart.$revealFrame);
        }
        chart.destroy();
    };

    const resetSeriesToggles = view => {
        view.querySelectorAll("[data-session-series-toggle]")
            .forEach(button => {
                button.classList.add("is-active");
                button.setAttribute("aria-pressed", "true");
            });
    };

    const createChart = canvas => {
        if (charts.has(canvas)) {
            return charts.get(canvas);
        }

        if (typeof window.Chart !== "function") {
            return null;
        }

        const labels = JSON.parse(canvas.dataset.labels || "[]");
        const previousValues = JSON.parse(
            canvas.dataset.previousValues || "[]");
        const currentValues = JSON.parse(
            canvas.dataset.currentValues || "[]");
        const averageValues = JSON.parse(
            canvas.dataset.averageValues || "[]");
        const chart = new window.Chart(canvas, {
            type: "line",
            plugins: [revealPlugin, tooltipCardPlugin],
            data: {
                labels,
                datasets: [
                    {
                        label: canvas.dataset.previousLabel,
                        data: previousValues,
                        borderColor: "#a43b70",
                        backgroundColor: "rgba(164, 59, 112, 0.12)",
                        borderWidth: 2.5,
                        fill: "origin",
                        pointBackgroundColor: "#a43b70",
                        pointBorderColor: "#ffffff",
                        pointBorderWidth: 2,
                        pointHoverRadius: 6,
                        pointRadius: 4,
                        tension: 0.28
                    },
                    {
                        label: canvas.dataset.currentLabel,
                        data: currentValues,
                        borderColor: "#4f87a5",
                        backgroundColor: "rgba(79, 135, 165, 0.14)",
                        borderWidth: 2.5,
                        fill: "origin",
                        pointBackgroundColor: "#4f87a5",
                        pointBorderColor: "#ffffff",
                        pointBorderWidth: 2,
                        pointHoverRadius: 6,
                        pointRadius: 4,
                        tension: 0.28
                    },
                    {
                        label: canvas.dataset.averageLabel,
                        data: averageValues,
                        borderColor: "#df944d",
                        backgroundColor: "#df944d",
                        borderDash: [6, 5],
                        borderWidth: 2,
                        fill: false,
                        pointBackgroundColor: "#df944d",
                        pointBorderColor: "#ffffff",
                        pointBorderWidth: 2,
                        pointHoverRadius: 5,
                        pointRadius: 3,
                        tension: 0.28
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                interaction: {
                    intersect: false,
                    mode: "index"
                },
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: "#ffffff",
                        borderColor: "rgba(31, 39, 44, 0.08)",
                        borderWidth: 1,
                        bodyColor: "#3d474d",
                        boxPadding: 4,
                        caretPadding: 8,
                        cornerRadius: 9,
                        padding: 11,
                        titleColor: "#20252a",
                        callbacks: {
                            label: context =>
                                `${context.dataset.label}: ${Number.isInteger(context.parsed.y)
                                    ? context.parsed.y
                                    : context.parsed.y.toFixed(1)} completed`
                        }
                    }
                },
                scales: {
                    x: {
                        border: {
                            color: "#cfd7db"
                        },
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: "#6c777e",
                            font: {
                                size: 11
                            }
                        }
                    },
                    y: {
                        beginAtZero: true,
                        suggestedMax: Number(
                            canvas.dataset.suggestedMaximum || 4),
                        border: {
                            display: false
                        },
                        grid: {
                            color: "#e2e7ea"
                        },
                        title: {
                            color: "#6c777e",
                            display: true,
                            text: "Completed sessions"
                        },
                        ticks: {
                            color: "#6c777e",
                            precision: 0
                        }
                    }
                }
            }
        });

        charts.set(canvas, chart);
        animateChartReveal(chart);
        return chart;
    };

    const showSelectedComparison = restartAnimation => {
        views.forEach(view => {
            const isSelected =
                view.dataset.sessionActivityView === selectedPeriod;
            view.hidden = !isSelected;

            if (isSelected) {
                const canvas = view.querySelector(
                    "[data-session-activity-chart]");
                if (restartAnimation && canvas && charts.has(canvas)) {
                    destroyChart(charts.get(canvas));
                    charts.delete(canvas);
                    resetSeriesToggles(view);
                }
                const chart = canvas ? createChart(canvas) : null;
                window.requestAnimationFrame(() => chart?.resize());
            }
        });
    };

    document.addEventListener("click", event => {
        const toggle = event.target.closest("[data-session-series-toggle]");
        if (!toggle) {
            return;
        }

        const view = toggle.closest("[data-session-activity-view]");
        const canvas = view?.querySelector("[data-session-activity-chart]");
        const chart = canvas ? createChart(canvas) : null;
        if (!chart) {
            return;
        }

        const datasetIndex = Number(toggle.dataset.sessionSeriesToggle);
        const willBeVisible = !chart.isDatasetVisible(datasetIndex);
        chart.setDatasetVisibility(datasetIndex, willBeVisible);
        toggle.classList.toggle("is-active", willBeVisible);
        toggle.setAttribute("aria-pressed", String(willBeVisible));
        chart.update();
    });

    filterTrigger.addEventListener("click", () => {
        const isOpening = filterMenu.hidden;
        setFilterOpen(isOpening);
        if (isOpening) {
            filter.querySelector(".admin-activity-filter-option.is-selected")
                ?.focus();
        }
    });

    filterOptions.forEach(option => {
        option.addEventListener("click", () => {
            const nextPeriod = option.dataset.sessionActivityOption;
            const periodChanged = nextPeriod !== selectedPeriod;
            selectedPeriod = nextPeriod;
            filterLabel.textContent = option.textContent.trim();
            filterOptions.forEach(item => {
                const isSelected = item === option;
                item.classList.toggle("is-selected", isSelected);
                item.setAttribute("aria-selected", String(isSelected));
            });
            setFilterOpen(false);
            filterTrigger.focus();
            if (periodChanged) {
                showSelectedComparison(true);
            }
        });
    });

    filter.addEventListener("keydown", event => {
        if (event.key === "Escape" && !filterMenu.hidden) {
            setFilterOpen(false);
            filterTrigger.focus();
        }
    });

    document.addEventListener("click", event => {
        if (!filter.contains(event.target)) {
            setFilterOpen(false);
        }
    });

    showSelectedComparison(false);
})();

(() => {
    "use strict";

    const carousel = document.querySelector("[data-admin-event-carousel]");
    if (!carousel) {
        return;
    }

    const slides = Array.from(
        carousel.querySelectorAll("[data-admin-event-slide]"));
    const dots = Array.from(
        carousel.querySelectorAll("[data-admin-event-dot]"));
    const previousButton = carousel.querySelector("[data-admin-event-previous]");
    const nextButton = carousel.querySelector("[data-admin-event-next]");
    const autoAdvanceDelay = 5000;
    let activeIndex = 0;
    let autoAdvanceTimer = null;

    if (slides.length < 2 || dots.length !== slides.length ||
        !previousButton || !nextButton) {
        return;
    }

    const showSlide = index => {
        activeIndex = (index + slides.length) % slides.length;
        slides.forEach((slide, slideIndex) => {
            const isActive = slideIndex === activeIndex;
            slide.classList.toggle("is-active", isActive);
            slide.setAttribute("aria-hidden", String(!isActive));
        });
        dots.forEach((dot, dotIndex) => {
            const isActive = dotIndex === activeIndex;
            dot.classList.toggle("is-active", isActive);
            dot.setAttribute("aria-current", String(isActive));
        });
    };

    const stopAutoAdvance = () => {
        if (autoAdvanceTimer) {
            window.clearInterval(autoAdvanceTimer);
            autoAdvanceTimer = null;
        }
    };

    const startAutoAdvance = () => {
        stopAutoAdvance();
        if (!window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            autoAdvanceTimer = window.setInterval(
                () => showSlide(activeIndex + 1),
                autoAdvanceDelay);
        }
    };

    previousButton.addEventListener("click", () => {
        showSlide(activeIndex - 1);
        startAutoAdvance();
    });

    nextButton.addEventListener("click", () => {
        showSlide(activeIndex + 1);
        startAutoAdvance();
    });

    dots.forEach((dot, index) => {
        dot.addEventListener("click", () => {
            showSlide(index);
            startAutoAdvance();
        });
    });

    carousel.addEventListener("mouseenter", stopAutoAdvance);
    carousel.addEventListener("mouseleave", startAutoAdvance);
    carousel.addEventListener("focusin", stopAutoAdvance);
    carousel.addEventListener("focusout", event => {
        if (!carousel.contains(event.relatedTarget)) {
            startAutoAdvance();
        }
    });

    showSlide(0);
    startAutoAdvance();
})();
