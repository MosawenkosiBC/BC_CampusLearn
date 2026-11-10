const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const scriptPath = path.join(__dirname,
    '../../BC_CampusLearn/wwwroot/js/components/navigation-drawer.js');
const partialPath = path.join(__dirname,
    '../../BC_CampusLearn/Pages/Shared/Components/NavigationDrawer/_NavigationDrawer.cshtml');

function element() {
    return {
        attributes: {},
        inert: true,
        listeners: {},
        addEventListener(name, handler) {
            (this.listeners[name] ??= []).push(handler);
        },
        setAttribute(name, value) { this.attributes[name] = value; },
        focus() { this.focused = true; }
    };
}

function setup() {
    const openButton = element();
    const closeButton = element();
    const backdrop = element();
    const panel = element();
    panel.querySelectorAll = () => [];

    const component = element();
    component.dataset = {};
    component.parentElement = null;
    component.classList = {
        values: new Set(),
        add(value) { this.values.add(value); },
        remove(value) { this.values.delete(value); },
        contains(value) { return this.values.has(value); }
    };
    component.querySelector = selector => ({
        '[data-drawer-open]': openButton,
        '[data-drawer-close]': closeButton,
        '[data-drawer-backdrop]': backdrop,
        '[data-drawer-panel]': panel
    })[selector];

    const body = {
        classList: component.classList,
        append(child) { child.parentElement = this; }
    };
    const documentListeners = {};
    const document = {
        body,
        activeElement: null,
        querySelectorAll() { return [component]; },
        addEventListener(name, handler) {
            (documentListeners[name] ??= []).push(handler);
        }
    };

    const context = { document, HTMLElement: class HTMLElement {} };
    const source = fs.readFileSync(scriptPath, 'utf8');
    vm.runInNewContext(source, context);
    vm.runInNewContext(source, context);

    return { component, openButton, closeButton, backdrop, panel,
        documentListeners };
}

test('the shared drawer loads its own behavior script', () => {
    const partial = fs.readFileSync(partialPath, 'utf8');
    assert.match(partial, /js\/components\/navigation-drawer\.js/);
});

test('drawer initialization is idempotent when a page also loads the script', () => {
    const ui = setup();
    assert.equal(ui.component.dataset.drawerInitialized, 'true');
    assert.equal(ui.openButton.listeners.click.length, 1);
    assert.equal(ui.closeButton.listeners.click.length, 1);
    assert.equal(ui.backdrop.listeners.click.length, 1);
    assert.equal(ui.documentListeners.keydown.length, 1);
});
