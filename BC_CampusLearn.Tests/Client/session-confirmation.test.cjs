const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname,
    '../../BC_CampusLearn/wwwroot/js/session-confirmation.js'), 'utf8');

function setup() {
    const handlers = {};
    const link = { value: 'https://example.com/manual', readOnly: false };
    const generator = {
        dataset: { generateUrl: '/Sessions?handler=GenerateTeamsLink' },
        disabled: false, hidden: false,
        addEventListener(event, handler) { handlers.generate = handler; }
    };
    const toggle = { disabled: false, hidden: false, setAttribute() {}, addEventListener() {} };
    const manual = { hidden: false };
    const error = { hidden: true };
    const progress = { hidden: true };
    const done = { disabled: false, addEventListener(event, handler) { handlers.done = handler; } };
    const message = {};
    const success = { hidden: true, querySelector(selector) { return selector === 'button' ? done : message; } };
    success.parentElement = { children: [manual, error, progress, success] };
    const footer = { hidden: false };
    const title = {};
    const form = {
        action: '/Sessions?handler=Confirm',
        closest() { return { addEventListener() {} }; },
        querySelector(selector) {
            return {
                '[data-confirm-error]': error,
                '[data-confirm-progress]': progress,
                "input[type='url']": link,
                '[data-generate-teams-link]': generator,
                '[data-manual-link-section]': manual,
                '[data-toggle-manual-link]': toggle,
                '[data-confirm-success]': success,
                '.modal-footer': footer,
                '.modal-header h2': title
            }[selector];
        },
        querySelectorAll(selector) { return selector === 'button' ? [generator, toggle, done] : []; },
        setAttribute() {}, removeAttribute() {},
        addEventListener(event, handler) { handlers[event] = handler; }
    };
    let respond;
    let requests = 0;
    let reloads = 0;
    vm.runInNewContext(source, {
        document: { getElementById() { return null; }, querySelectorAll() { return [form]; } },
        FormData: class {},
        fetch() {
            requests++;
            return new Promise(resolve => { respond = result => resolve({
                ok: true, headers: { get() { return 'application/json'; } },
                async json() { return result; }
            }); });
        },
        window: { location: { reload() { reloads++; } } }
    });
    return { handlers, link, generator, toggle, manual, error, progress, success, done, message, footer, title,
        respond(result) { respond(result); },
        get requests() { return requests; }, get reloads() { return reloads; } };
}

test('generation confirms once and displays success in the open modal until Done', async () => {
    const ui = setup();
    const pending = ui.handlers.generate();
    assert.equal(ui.link.readOnly, true);
    assert.equal(ui.toggle.disabled, true);
    ui.respond({ success: true, link: 'https://teams.microsoft.com/generated' });
    await pending;
    assert.equal(ui.link.value, 'https://teams.microsoft.com/generated');
    assert.equal(ui.link.readOnly, true);
    assert.equal(ui.toggle.hidden, true);
    assert.equal(ui.manual.hidden, true);
    assert.equal(ui.generator.disabled, true);
    assert.equal(ui.reloads, 0);
    assert.equal(ui.success.hidden, false);
    assert.equal(ui.footer.hidden, true);
    assert.equal(ui.done.disabled, false);
    assert.equal(ui.title.textContent, 'Session confirmed');
    assert.match(ui.message.textContent, /session confirmed/);
    await ui.handlers.submit({ preventDefault() {} });
    assert.equal(ui.requests, 1);
    ui.handlers.done();
    assert.equal(ui.reloads, 1);
});

test('failed generation restores manual entry without reloading', async () => {
    const ui = setup();
    const pending = ui.handlers.generate();
    ui.respond({ success: false, message: 'Teams is unavailable. Retry.' });
    await pending;
    assert.equal(ui.link.readOnly, false);
    assert.equal(ui.link.value, 'https://example.com/manual');
    assert.equal(ui.toggle.disabled, false);
    assert.equal(ui.generator.disabled, false);
    assert.equal(ui.error.hidden, false);
    assert.equal(ui.reloads, 0);
});
