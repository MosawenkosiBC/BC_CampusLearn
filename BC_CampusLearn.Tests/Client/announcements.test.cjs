const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../BC_CampusLearn/wwwroot/js/announcements.js'), 'utf8');

function setup(valid = true, editorText = null) {
    const progress = { hidden: true };
    const button = { disabled: false, textContent: 'Send announcement' };
    const content = { value: '' };
    const error = { textContent: '' };
    const attributes = new Map();
    let submit, restore;
    const form = {
        querySelector: selector => ({
            '[data-announcement-content]': content,
            '[data-announcement-editor]': {},
            '[data-announcement-validation]': error,
            '[data-announcement-audience-help]': {},
            '[data-announcement-progress]': progress,
            'button[type="submit"]': button
        })[selector],
        querySelectorAll: () => [],
        addEventListener: (_, handler) => { submit = handler; },
        checkValidity: () => valid,
        setAttribute: (name, value) => attributes.set(name, value),
        removeAttribute: name => attributes.delete(name)
    };
    const window = { addEventListener: (_, handler) => { restore = handler; } };
    if (editorText !== null) {
        window.Quill = true;
        window.createResourceRichTextEditor = () => ({
            root: { setAttribute() {} }, on() {}, focus() {},
            getText: () => editorText, getContents: () => ({ ops: [{ insert: editorText }] })
        });
    }
    vm.runInNewContext(source, { document: { querySelector: () => form }, window });
    return { progress, button, attributes, error, restore, submit: () => {
        const event = { defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
        submit(event);
        return event;
    } };
}

test('Submitting shows progress, prevents repeat submission, and resets on return', () => {
    const page = setup();
    assert.equal(page.submit().defaultPrevented, false);
    assert.equal(page.progress.hidden, false);
    assert.equal(page.button.disabled, true);
    assert.equal(page.attributes.get('aria-busy'), 'true');
    assert.equal(page.submit().defaultPrevented, true);
    page.restore();
    assert.equal(page.progress.hidden, true);
    assert.equal(page.button.disabled, false);
    assert.equal(page.attributes.has('aria-busy'), false);
    assert.equal(page.submit().defaultPrevented, false);
});

test('Invalid form does not show sending progress', () => {
    const page = setup(false);
    page.submit();
    assert.equal(page.progress.hidden, true);
    assert.equal(page.button.disabled, false);
});

test('Empty rich text prevents submission without showing progress', () => {
    const page = setup(true, '\n');
    assert.equal(page.submit().defaultPrevented, true);
    assert.equal(page.error.textContent, 'Enter an announcement message.');
    assert.equal(page.progress.hidden, true);
    assert.equal(page.button.disabled, false);
});
