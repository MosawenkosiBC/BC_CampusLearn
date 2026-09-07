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
                            font: { size: 11, weight: "600" }
                        },
                        ticks: {
                            color: "#858b94",
                            font: { size: 10 },
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
                            font: { size: 11, weight: "600" }
                        },
                        ticks: {
                            color: "#858b94",
                            font: { size: 10 },
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
