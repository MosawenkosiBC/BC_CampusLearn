const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../BC_CampusLearn/wwwroot/js/teams-organizer-settings.js'), 'utf8');

function setup(fetch) {
    const classes = new Set();
    const status = { hidden: true, textContent: '', classList: {
        add: name => classes.add(name), remove: name => classes.delete(name),
        toggle: (name, enabled) => enabled ? classes.add(name) : classes.delete(name)
    } };
    const origin = { textContent: 'server environment' };
    const error = { hidden: false };
    const validation = { hidden: false };
    const inputs = new Map(['TeamsOrganizer.Email', 'TeamsOrganizer.ObjectId'].map(name => [name, { value: 'posted value' }]));
    const buttons = [{ disabled: false }, { disabled: false }];
    const card = { open: false, querySelector: selector => ({
        '[data-teams-organizer-status]': status,
        '[data-teams-organizer-source]': origin,
        '[data-teams-organizer-configuration-error]': error
    })[selector] };
    let submit;
    const form = {
        action: '/Administrator/Settings/Bookings?handler=SaveTeamsOrganizer',
        closest: () => card, querySelectorAll: () => buttons, querySelector: () => validation,
        elements: { namedItem: name => inputs.get(name) },
        setAttribute() {}, removeAttribute() {}, addEventListener: (_, handler) => { submit = handler; }
    };
    class FormData { constructor(input) { assert.equal(input, form); this.token = 'antiforgery'; } }
    vm.runInNewContext(source, { document: { querySelector: () => form }, fetch, FormData, TypeError });
    return { status, origin, error, buttons, inputs, classes, submit: (testing = false) => submit({
        preventDefault() {}, submitter: {
            matches: () => testing,
            formAction: `/Administrator/Settings/Bookings?handler=${testing ? 'Test' : 'Save'}TeamsOrganizer`
        }
    }) };
}

function response(result, status = 200, type = 'application/json') {
    return { ok: status < 400, status, headers: { get: () => type }, json: async () => result };
}

test('Save posts to its handler with the form token and shows the persisted account', async () => {
    const ui = setup(async (url, options) => {
        assert.match(url, /handler=SaveTeamsOrganizer$/);
        assert.equal(options.body.token, 'antiforgery');
        assert.equal(options.headers.Accept, 'application/json');
        return response({ succeeded: true, message: 'Account saved.', source: 'saved Super Admin settings', email: 'account@example.com', objectId: 'id' });
    });
    await ui.submit();
    assert.equal(ui.status.textContent, 'Account saved.');
    assert.equal(ui.status.hidden, false);
    assert.equal(ui.origin.textContent, 'saved Super Admin settings');
    assert.equal(ui.error.hidden, true);
    assert.equal(ui.inputs.get('TeamsOrganizer.ObjectId').value, 'id');
});

test('Test uses its own handler, shows pending status, and prevents duplicate requests', async () => {
    let finish;
    let calls = 0;
    const ui = setup(url => {
        calls++;
        assert.match(url, /handler=TestTeamsOrganizer$/);
        return new Promise(resolve => { finish = resolve; });
    });
    const request = ui.submit(true);
    assert.match(ui.status.textContent, /Testing/);
    assert.ok(ui.buttons.every(button => button.disabled));
    await ui.submit(true);
    assert.equal(calls, 1);
    finish(response({ succeeded: true, message: 'Calendar succeeded. Nothing saved.' }));
    await request;
    assert.match(ui.status.textContent, /succeeded/);
    assert.equal(ui.origin.textContent, 'server environment');
    assert.equal(ui.error.hidden, false);
    assert.ok(ui.buttons.every(button => !button.disabled));
});

test('Microsoft and validation failures are visible and preserve the entered values', async () => {
    const ui = setup(async () => response({ succeeded: false, message: 'Microsoft denied calendar access.' }, 400));
    await ui.submit(true);
    assert.match(ui.status.textContent, /denied calendar access/);
    assert.ok(ui.classes.has('text-danger'));
    assert.equal(ui.inputs.get('TeamsOrganizer.ObjectId').value, 'posted value');
    assert.ok(ui.buttons.every(button => !button.disabled));
});

test('Expired sign-in and server errors produce a visible result rather than a silent click', async () => {
    for (const status of [200, 400, 500]) {
        const ui = setup(async () => response(null, status, 'text/html'));
        await ui.submit();
        assert.match(ui.status.textContent, status === 500 ? /application logs/ : status === 400 ? /could not be verified/ : /sign-in may have expired/);
        assert.ok(ui.classes.has('text-danger'));
        assert.ok(ui.buttons.every(button => !button.disabled));
    }
});

test('Network failure leaves the form usable and tells the user to check the saved state', async () => {
    const ui = setup(async () => { throw new TypeError('network'); });
    await ui.submit();
    assert.match(ui.status.textContent, /refresh to check whether the account was saved/);
    assert.ok(ui.buttons.every(button => !button.disabled));
});
