const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../BC_CampusLearn/wwwroot/js/session-confirmation.js'), 'utf8');

function setup(fetch, { generate = false } = {}) {
    const handlers = {};
    const modalHandlers = {};
    const error = { textContent: '', hidden: true };
    const progress = { textContent: '', hidden: true };
    const link = { value: '', readOnly: true, focus() { this.focused = true; }, addEventListener() {} };
    const generationHandlers = {};
    const generator = generate ? { hidden: false, disabled: false, dataset: { generateUrl: '/Tutors/Sessions?handler=GenerateTeamsLink' },
        classList: { toggle() {} }, addEventListener(name, handler) { generationHandlers[name] = handler; } } : null;
    const row = { dataset: { bookingId: '12' } };
    const buttons = [{ disabled: false }, { disabled: false }];
    const modal = { addEventListener(name, handler) { modalHandlers[name] = handler; } };
    const attributes = {};
    const form = {
        action: '/Tutors/Sessions?handler=Confirm', valid: true,
        closest() { return modal; },
        querySelector(selector) {
            return selector === '[data-confirm-error]' ? error : selector === '[data-confirm-progress]' ? progress : selector === '[data-generate-teams-link]' ? generator : link;
        },
        querySelectorAll() { return buttons; },
        reportValidity() { return this.valid; },
        addEventListener(name, handler) { handlers[name] = handler; },
        setAttribute(name, value) { attributes[name] = value; },
        removeAttribute(name) { delete attributes[name]; }
    };
    let reloads = 0;
    const calls = [];
    class FormData {
        constructor() {
            this.meetingLink = link.value;
            this.bookingId = '12';
            this.__RequestVerificationToken = 'test-antiforgery-token';
        }
        get(name) { return this[name]; }
    }
    vm.runInNewContext(source, {
        document: { querySelectorAll(selector) { return selector === "[data-tutor-confirm-session]" ? [row] : [form]; } },
        FormData,
        fetch(url, options) { calls.push({ url, options }); return fetch(url, options); },
        window: { location: { reload() { reloads++; } } }
    });
    return {
        form, link, error, progress, buttons, calls, attributes, generator, row,
        generate() { return generationHandlers.click(); },
        submit() { return handlers.submit({ preventDefault() {} }); },
        hide() { let prevented = false; modalHandlers['hide.bs.modal']({ preventDefault() { prevented = true; } }); return prevented; },
        show() { modalHandlers['show.bs.modal'](); },
        get reloads() { return reloads; }
    };
}

function response(payload, { ok = true, type = 'application/json' } = {}) {
    return { ok, headers: { get() { return type; } }, async json() { return payload; } };
}

test('automatic confirmation posts booking and antiforgery data, then reloads on success', async () => {
    const state = setup(async () => response({ success: true }));
    await state.submit();
    assert.equal(state.calls.length, 1);
    const request = state.calls[0].options;
    assert.equal(request.method, 'POST');
    assert.equal(request.credentials, 'same-origin');
    assert.equal(request.headers.Accept, 'application/json');
    assert.equal(request.body.meetingLink, '');
    assert.equal(request.body.__RequestVerificationToken, 'test-antiforgery-token');
    assert.equal(state.reloads, 1);
});

test('a failed Teams request stays in the modal and can be followed by manual confirmation', async () => {
    let failed = true;
    const state = setup(async () => response(failed
        ? { success: false, message: 'Microsoft failed. Enter a manual meeting link.' }
        : { success: true }));
    await state.submit();
    assert.equal(state.reloads, 0);
    assert.equal(state.error.hidden, false);
    assert.match(state.error.textContent, /manual meeting link/);
    assert.equal(state.link.readOnly, false);
    assert.equal(state.link.focused, true);
    assert.equal(state.progress.hidden, true);
    assert.ok(state.buttons.every(button => !button.disabled));
    state.link.value = 'https://example.com/manual';
    failed = false;
    await state.submit();
    assert.equal(state.calls[1].options.body.meetingLink, 'https://example.com/manual');
    assert.equal(state.reloads, 1);
});

test('pending submission blocks duplicate clicks and closing the modal', async () => {
    let finish;
    const state = setup(() => new Promise(resolve => { finish = resolve; }));
    const first = state.submit();
    await state.submit();
    assert.equal(state.calls.length, 1);
    assert.equal(state.hide(), true);
    assert.equal(state.attributes['aria-busy'], 'true');
    assert.ok(state.buttons.every(button => button.disabled));
    assert.equal(state.progress.hidden, false);
    finish(response({ success: false, message: 'Retry.' }));
    await first;
    assert.equal(state.hide(), false);
    assert.equal(state.attributes['aria-busy'], undefined);
});

test('expired sign-in response requires reload instead of claiming successful confirmation', async () => {
    const state = setup(async () => response({}, { type: 'text/html' }));
    await state.submit();
    assert.equal(state.reloads, 0);
    assert.equal(state.error.hidden, false);
    assert.match(state.error.textContent, /Reload the page/);
});

test('invalid manual URL is not submitted and reopening clears a previous error', async () => {
    const state = setup(async () => response({ success: true }));
    state.form.valid = false;
    await state.submit();
    assert.equal(state.calls.length, 0);
    state.error.textContent = 'Earlier failure';
    state.error.hidden = false;
    state.show();
    assert.equal(state.error.hidden, true);
    assert.equal(state.error.textContent, '');
});

test('generation fills and locks the link, hides its button, and caches it without confirming', async () => {
    const state = setup(async () => response({ success: true, link: 'https://teams.microsoft.com/generated' }), { generate: true });
    await state.generate();
    assert.match(state.calls[0].url, /handler=GenerateTeamsLink/);
    assert.equal(state.link.value, 'https://teams.microsoft.com/generated');
    assert.equal(state.link.readOnly, true);
    assert.equal(state.generator.hidden, true);
    assert.equal(state.reloads, 0);
    assert.equal(state.row.dataset.meetingLink, state.link.value);
    state.show();
    assert.equal(state.generator.hidden, true);
    await state.generate();
    assert.equal(state.calls.length, 1);
});

test('failed generation retains its button and permits manual fallback', async () => {
    const state = setup(async () => response({ success: false, message: 'Microsoft is unavailable.' }), { generate: true });
    await state.generate();
    assert.equal(state.generator.hidden, false);
    assert.equal(state.link.readOnly, false);
    assert.equal(state.error.hidden, false);
    assert.equal(state.reloads, 0);
});
