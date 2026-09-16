document.querySelectorAll('[data-copy-value]').forEach(button => {
    button.addEventListener('click', async () => {
        const status = document.querySelector('.admin-profile-copy-status');
        try {
            await navigator.clipboard.writeText(button.dataset.copyValue);
            status.textContent = 'Copied to clipboard.';
        } catch {
            status.textContent = 'Unable to copy. Select and copy the text manually.';
        }
    });
});

const customDateModal = document.getElementById('custom-date-modal');
if (customDateModal) {
    const startDate = document.getElementById('StartDate');
    const endDate = document.getElementById('EndDate');
    const validateDates = () => {
        endDate.setCustomValidity(startDate.value && endDate.value && endDate.value < startDate.value
            ? 'Choose an end date on or after the start date.' : '');
    };
    startDate.addEventListener('input', validateDates);
    endDate.addEventListener('input', validateDates);
    customDateModal.addEventListener('shown.bs.modal', () => {
        validateDates();
        startDate.focus();
    });
    customDateModal.addEventListener('hidden.bs.modal', () => {
        document.getElementById('custom-date-form').reset();
        validateDates();
    });
    if (customDateModal.dataset.dateError === 'true') {
        bootstrap.Modal.getOrCreateInstance(customDateModal).show();
    }
}

const pieWrapper = document.querySelector('.admin-module-pie-wrapper');
if (pieWrapper) {
    const tooltip = pieWrapper.querySelector('.admin-module-tooltip');
    const hideTooltip = () => { tooltip.hidden = true; };
    pieWrapper.querySelectorAll('[data-pie-tooltip]').forEach(slice => {
        const showTooltip = () => {
            tooltip.textContent = slice.dataset.pieTooltip;
            tooltip.hidden = false;
        };
        slice.addEventListener('pointerenter', showTooltip);
        slice.addEventListener('pointerleave', hideTooltip);
        slice.addEventListener('focus', showTooltip);
        slice.addEventListener('blur', hideTooltip);
        slice.addEventListener('click', showTooltip);
        slice.addEventListener('keydown', event => {
            if (event.key === 'Escape') hideTooltip();
        });
    });
}
