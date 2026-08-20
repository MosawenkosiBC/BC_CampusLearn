(() => {
    "use strict";

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

    const trendCanvas = document.querySelector("[data-tutor-trend-chart]");

    if (trendCanvas) {
        const labels = parseValues(trendCanvas, "labels");
        const values = parseValues(trendCanvas, "values");

        new window.Chart(trendCanvas, {
            type: "line",
            data: {
                labels,
                datasets: [{
                    data: values,
                    borderColor: "#ad0151",
                    backgroundColor: "rgba(173, 1, 81, 0.08)",
                    borderWidth: 2.25,
                    pointBackgroundColor: "#ffffff",
                    pointBorderColor: "#ad0151",
                    pointBorderWidth: 2,
                    pointRadius: 3.5,
                    pointHoverRadius: 5,
                    tension: 0.35,
                    fill: true
                }]
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
                        displayColors: false,
                        callbacks: {
                            label: (context) => {
                                const count = context.parsed.y;
                                return `${count} ${count === 1 ? "session" : "sessions"}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: {
                            color: "#858b94",
                            font: { size: 10 },
                            maxRotation: 0
                        },
                        border: { display: false }
                    },
                    y: {
                        beginAtZero: true,
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

    const statusCanvas = document.querySelector("[data-tutor-status-chart]");

    if (statusCanvas) {
        const labels = parseValues(statusCanvas, "labels");
        const values = parseValues(statusCanvas, "values");
        const hasData = values.some((value) => value > 0);

        new window.Chart(statusCanvas, {
            type: "doughnut",
            data: {
                labels: hasData ? labels : ["No sessions"],
                datasets: [{
                    data: hasData ? values : [1],
                    backgroundColor: hasData
                        ? ["#4aa665", "#ad0151", "#4ac1c1", "#e07a89", "#9a9fa7"]
                        : ["#eceef1"],
                    borderWidth: 0,
                    hoverOffset: hasData ? 4 : 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "72%",
                plugins: {
                    legend: { display: false },
                    tooltip: { enabled: hasData }
                }
            }
        });
    }
})();
