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
