(() => {
    const form = document.querySelector('[data-teams-organizer-form]');
    if (!form) return;
    const card = form.closest('details');
    const status = card.querySelector('[data-teams-organizer-status]');
    let pending = false;

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (pending) return;
        pending = true;
        const testing = event.submitter?.matches('[data-teams-organizer-test]');
        const action = event.submitter?.formAction || form.action;
        const body = new FormData(form);
        const buttons = form.querySelectorAll('button[type="submit"]');
        buttons.forEach(button => { button.disabled = true; });
        form.setAttribute('aria-busy', 'true');
        card.open = true;
        status.hidden = false;
        status.classList.remove('text-danger');
        status.textContent = testing ? 'Testing calendar connection…' : 'Saving meeting account…';
        form.querySelector('[data-teams-organizer-validation]').hidden = true;
        try {
            const response = await fetch(action, {
                method: 'POST', body, credentials: 'same-origin',
                headers: { Accept: 'application/json' }
            });
            if (!response.headers.get('content-type')?.includes('application/json')) {
                throw new Error(response.status === 400
                    ? 'The request could not be verified. Refresh this page and try again.'
                    : response.status >= 500
                    ? 'The server could not complete the request. Ask the administrator to check the application logs and database migrations.'
                    : 'The server did not return an account result. Your sign-in may have expired. Refresh the page and try again.');
            }
            const result = await response.json();
            status.textContent = result.message || 'The request could not be completed.';
            status.classList.toggle('text-danger', !response.ok || !result.succeeded);
            if (response.ok && result.succeeded && !testing) {
                card.querySelector('[data-teams-organizer-source]').textContent = result.source;
                card.querySelector('[data-teams-organizer-configuration-error]').hidden = true;
                form.elements.namedItem('TeamsOrganizer.Email').value = result.email;
                form.elements.namedItem('TeamsOrganizer.ObjectId').value = result.objectId;
            }
        } catch (error) {
            status.classList.add('text-danger');
            status.textContent = error instanceof TypeError
                ? 'The server could not be reached. Check your connection, then retry. If saving, refresh to check whether the account was saved.'
                : error.message;
        } finally {
            buttons.forEach(button => { button.disabled = false; });
            form.removeAttribute('aria-busy');
            pending = false;
        }
    });
})();
