const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const script = fs.readFileSync(path.join(__dirname, '../../BC_CampusLearn/wwwroot/js/tutor-onboarding.js'), 'utf8');

function load({ storage = new Map(), loginId = 'login-1', withModal = true } = {}) {
    const listeners = {};
    const modalListeners = {};
    const attributes = new Map();
    const bio = {
        value: '', focused: false,
        addEventListener(name, handler) { listeners[`bio:${name}`] = handler; },
        setAttribute(name, value) { attributes.set(name, value); },
        removeAttribute(name) { attributes.delete(name); },
        focus() { this.focused = true; }
    };
    const bioError = { hidden: true, textContent: '' };
    const errors = { hidden: true, focus() {}, replaceChildren() {} };
    const button = { disabled: false, textContent: 'Save', setAttribute() {}, removeAttribute() {} };
    const form = {
        action: '/Tutors/Onboarding',
        querySelector(selector) {
            return selector === "button[type='submit']" ? button
                : selector === '.tutor-onboarding-errors' ? errors
                : selector === "[name='Input.Biography']" ? bio : bioError;
        },
        addEventListener(name, handler) { listeners[name] = handler; }
    };
    const state = { shown: 0, requests: 0, destinations: [] };
    const modal = {
        dataset: { onboardingStorageKey: 'campuslearn:tutor-onboarding:1', onboardingLoginId: loginId },
        addEventListener(name, handler) { modalListeners[name] = handler; },
        querySelector() { return bio; }
    };
    const bootstrap = { Modal: { getOrCreateInstance() { return { show() { state.shown++; } }; } } };
    const context = {
        document: {
            readyState: 'complete',
            getElementById() { return withModal ? modal : null; },
            querySelector() { return null; },
            querySelectorAll() { return [form]; },
            createElement() { return {}; }
        },
        bootstrap,
        window: { bootstrap, location: { assign(url) { state.destinations.push(url); } } },
        localStorage: {
            getItem(key) { return storage.get(key) ?? null; },
            setItem(key, value) { storage.set(key, value); }
        },
        FormData: class {},
        async fetch() {
            state.requests++;
            return {
                ok: true, headers: { get() { return 'application/json'; } },
                async json() { return { nextUrl: '/Tutors/ManageAvailability' }; }
            };
        }
    };
    vm.runInNewContext(script, context);
    return { state, bio, bioError, attributes, button, modalListeners, listeners,
        submit: () => listeners.submit({ preventDefault() {} }) };
}

test('closing the prompt suppresses it on later pages, but the next login shows it again', () => {
    const storage = new Map();
    const firstPage = load({ storage });
    assert.equal(firstPage.state.shown, 1);
    firstPage.modalListeners['hidden.bs.modal']();
    assert.equal(load({ storage }).state.shown, 0);
    assert.equal(load({ storage, loginId: 'login-2' }).state.shown, 1);
});

test('empty, whitespace-only and short bios show an inline error without submitting', async () => {
    for (const value of ['', '   ', 'a'.repeat(29), '   ' + 'a'.repeat(29) + '   ', 'a'.repeat(501)]) {
        const page = load();
        page.bio.value = value;
        await page.submit();
        assert.equal(page.state.requests, 0);
        assert.equal(page.bioError.hidden, false);
        assert.ok(page.bioError.textContent);
        assert.equal(page.bio.focused, true);
        assert.equal(page.attributes.get('aria-invalid'), 'true');
        assert.equal(page.button.disabled, false);
        assert.deepEqual(page.state.destinations, []);
    }
});

test('a corrected bio of at least 30 characters can be submitted', async () => {
    const page = load();
    await page.submit();
    page.bio.value = 'a'.repeat(30);
    page.listeners['bio:input']();
    assert.equal(page.bioError.hidden, true);
    await page.submit();
    assert.equal(page.state.requests, 1);
    assert.deepEqual(page.state.destinations, ['/Tutors/ManageAvailability']);
});

test('the standalone onboarding form also validates and accepts the upper length boundary', async () => {
    const page = load({ withModal: false });
    await page.submit();
    assert.equal(page.state.requests, 0);
    page.bio.value = 'a'.repeat(500);
    await page.submit();
    assert.equal(page.state.requests, 1);
});
