(() => {
    const card = document.querySelector("[data-expired-booking='true']");
    const countdown = card?.querySelector(
        "[data-booking-redirect-countdown]");
    const redirectUrl = card?.dataset.redirectUrl;
    let remainingSeconds = Number(card?.dataset.redirectSeconds ?? 10);

    if (!card || !countdown || !redirectUrl) {
        return;
    }

    const updateCountdown = () => {
        const unit = remainingSeconds === 1 ? "second" : "seconds";
        countdown.textContent = `${remainingSeconds} ${unit}`;
    };

    updateCountdown();

    const timer = window.setInterval(() => {
        remainingSeconds -= 1;

        if (remainingSeconds <= 0) {
            window.clearInterval(timer);
            window.location.assign(redirectUrl);
            return;
        }

        updateCountdown();
    }, 1000);
})();
