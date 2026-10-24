const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function load({ withHeadConfirmation = false } = {}) {
    const listeners = {};
    const element = id => ({
        checked: false, disabled: false, textContent: '', value: '',
        classList: { contains() { return false; }, add() {}, remove() {} },
        setAttribute() {}, removeAttribute() {}, focus() {},
        matches() { return false; },
        addEventListener(event, callback) { listeners[`${id}:${event}`] = callback; }
    });
    const senior = element('senior');
    const head = element('head');
    const confirm = element('confirm');
    const summary = element('summary');
    const modal = element('modal');
    const headModal = element('headModal');
    const headConfirm = element('headConfirm');
    const rejectionModal = element('rejectionModal');
    const rejectionTrigger = element('reject');
    const note = element('note');
    const rejectionNote = element('rejectionNote');
    const error = element('error');
    const form = element('form');
    form.querySelector = selector => selector === '#ReviewNote' ? note : error;
    const calls = [];
    form.requestSubmit = submitter => {
        let prevented = false;
        listeners['form:submit']({ submitter, preventDefault() { prevented = true; } });
        if (!prevented) calls.push(['submit', submitter]);
    };
    const elements = {
        'approve-senior-application-modal': modal,
        'approve-senior-toggle': senior,
        'approve-head-toggle': head,
        'senior-approval-summary': summary,
        'reject-senior-application-modal': rejectionModal,
        'senior-rejection-note': rejectionNote,
        'senior-rejection-error': error,
        'senior-application-decision-form': form
    };
    if (withHeadConfirmation) elements['confirm-head-approval-modal'] = headModal;
    vm.runInNewContext(fs.readFileSync(path.join(__dirname,
        '../../BC_CampusLearn/wwwroot/js/admin-senior-tutor-application.js'), 'utf8'), {
        document: {
            addEventListener(event, callback) { callback(); },
            getElementById(id) { return elements[id] ?? null; },
            querySelectorAll() { return [rejectionTrigger]; },
            querySelector(selector) {
                return selector === '[data-senior-approve-confirm]' ? confirm
                    : selector === '[data-head-selection-confirm]' && withHeadConfirmation ? headConfirm : null;
            }
        }, window: { bootstrap: { Modal: { getOrCreateInstance(modal) {
            return { show() { calls.push(['show', modal]); }, hide() { calls.push(['hide', modal]); } };
        } } } }
    });
    return { senior, head, confirm, summary, listeners, calls, modal, headModal, rejectionModal, note, rejectionNote, error };
}

test('approval starts with both roles off and confirmation disabled', () => {
    const { senior, head, confirm } = load();
    assert.equal(senior.checked, false);
    assert.equal(head.checked, false);
    assert.equal(head.disabled, true);
    assert.equal(confirm.disabled, true);
    assert.equal(load().summary.textContent, '');
});

test('Head confirmation appears only on final approval and confirming submits once', () => {
    const { senior, head, confirm, modal, headModal, listeners, calls } = load({ withHeadConfirmation: true });
    senior.checked = true;
    listeners['senior:change']();
    head.checked = true;
    listeners['head:change']();
    assert.deepEqual(calls, []);
    let prevented = false;
    listeners['form:submit']({ submitter: confirm, preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
    assert.deepEqual(calls, [['hide', modal]]);
    listeners['modal:hidden.bs.modal']();
    assert.deepEqual(calls[1], ['show', headModal]);
    listeners['headConfirm:click']();
    assert.deepEqual(calls[2], ['hide', headModal]);
    assert.deepEqual(calls[3], ['submit', confirm]);
    listeners['headModal:hidden.bs.modal']();
    assert.equal(calls.length, 4);
    assert.equal(senior.checked, true);
    assert.equal(head.checked, true);
});

test('going back from final confirmation preserves both choices without submitting', () => {
    const { senior, head, confirm, listeners, calls } = load({ withHeadConfirmation: true });
    senior.checked = true;
    listeners['senior:change']();
    head.checked = true;
    listeners['head:change']();
    listeners['form:submit']({ submitter: confirm, preventDefault() {} });
    listeners['modal:hidden.bs.modal']();
    listeners['headModal:hidden.bs.modal']();
    listeners['modal:show.bs.modal']();
    assert.equal(senior.checked, true);
    assert.equal(head.checked, true);
    assert.equal(calls.some(call => call[0] === 'submit'), false);
});

test('Senior-only approval submits directly without Head confirmation', () => {
    const { senior, confirm, listeners, calls } = load({ withHeadConfirmation: true });
    senior.checked = true;
    listeners['senior:change']();
    let prevented = false;
    listeners['form:submit']({ submitter: confirm, preventDefault() { prevented = true; } });
    assert.equal(prevented, false);
    assert.deepEqual(calls, []);
});

test('reject opens confirmation even without a reason and waits for approval modal to close', () => {
    const { modal, rejectionModal, listeners, calls } = load();
    modal.classList.contains = () => true;
    listeners['reject:click']({ preventDefault() {} });
    assert.deepEqual(calls, [['hide', modal]]);
    listeners['modal:hidden.bs.modal']();
    assert.deepEqual(calls, [['hide', modal], ['show', rejectionModal]]);
});

test('rejection requires a reason in confirmation and copies it to the submitted decision note', () => {
    const { listeners, rejectionNote, note, error } = load();
    let prevented = false;
    const event = { submitter: { matches() { return true; } }, preventDefault() { prevented = true; } };
    listeners['form:submit'](event);
    assert.equal(prevented, true);
    assert.match(error.textContent, /Enter a reason/);
    rejectionNote.value = 'Please gain more leadership experience.';
    listeners['rejectionNote:input']();
    prevented = false;
    listeners['form:submit'](event);
    assert.equal(prevented, false);
    assert.equal(note.value, rejectionNote.value);
    assert.equal(error.textContent, '');
});

test('head approval depends on senior and choices reset each time the modal opens', () => {
    const { senior, head, confirm, summary, listeners } = load();
    senior.checked = true;
    listeners['senior:change']();
    assert.equal(confirm.disabled, false);
    assert.equal(head.disabled, false);
    assert.match(summary.textContent, /Head of Tutor will be declined/);
    head.checked = true;
    listeners['head:change']();
    assert.match(summary.textContent, /both be approved/);
    senior.checked = false;
    listeners['senior:change']();
    assert.equal(head.checked, false);
    assert.equal(confirm.disabled, true);
    senior.checked = true;
    head.checked = true;
    listeners['modal:show.bs.modal']();
    assert.equal(senior.checked, false);
    assert.equal(head.checked, false);
    assert.equal(confirm.disabled, true);
});
