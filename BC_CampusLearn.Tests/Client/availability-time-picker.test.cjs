const test = require('node:test');
const assert = require('node:assert/strict');
const { parts, valueOf, labelOf } = require('../../BC_CampusLearn/wwwroot/js/availability-time-picker.js');

test('all 1,440 minute values retain their 24-hour time', () => {
    for (let hour = 0; hour < 24; hour++) {
        for (let minute = 0; minute < 60; minute++) {
            const value = `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`;
            assert.equal(valueOf(parts(value)), value);
        }
    }
});

test('midnight, noon and afternoon have unambiguous labels', () => {
    assert.equal(labelOf('00:00'), '00:00');
    assert.equal(labelOf('12:00'), '12:00');
    assert.equal(labelOf('14:30'), '14:30');
    assert.equal(labelOf('23:59'), '23:59');
    assert.equal(labelOf('09:15:00.000'), '09:15');
});

test('empty fields and invalid times cannot produce a valid selection', () => {
    for (const value of ['', null, '24:00', '12:60', '9:00', 'invalid']) {
        assert.equal(parts(value), null);
        assert.equal(labelOf(value), 'Choose time');
    }
    assert.equal(valueOf({ hour: 24, minute: 0 }), '');
    assert.equal(valueOf({ hour: 9, minute: 60 }), '');
});
