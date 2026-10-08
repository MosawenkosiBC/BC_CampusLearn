const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
let chromium;
try { ({ chromium } = require('playwright')); } catch { /* Browser checks require Playwright. */ }

test('plus opens the 24-hour wheels and Done adds one validated slot', { skip: !chromium }, async () => {
    const browser = await chromium.launch({ headless: true, channel: 'msedge' });
    try {
        const page = await browser.newPage({ viewport: { width: 800, height: 650 } });
        await page.setContent(`<style>body{font-family:Arial;padding:40px}main{width:280px}.row{display:flex;margin:12px 0}</style>
            <main class="manage-availability-page"><form>
            <div class="row"><input id="recurring-time" type="time"><button type="button" id="recurring-add">+</button></div>
            <div class="row"><input id="specific-time" type="time"><button type="button" id="specific-add">Add time slot</button></div>
            <p id="error"></p></form></main>`);
        await page.addStyleTag({ path: path.resolve(__dirname, '../../BC_CampusLearn/wwwroot/css/availability-time-picker.css') });
        const manage = fs.readFileSync(path.resolve(__dirname, '../../BC_CampusLearn/wwwroot/js/manage-availability.js'), 'utf8');
        const section = (start, end) => {
            const first = manage.indexOf(start);
            const last = manage.indexOf(end, first);
            assert.ok(first >= 0 && last > first);
            return manage.slice(first, last);
        };
        // Use the actual slot-add handlers; only page rendering and date context are stubbed.
        await page.addScriptTag({ content: `(() => {
            const specificTimeInput = document.querySelector('#specific-time');
            const specificAddButton = document.querySelector('#specific-add');
            const slotTimeInput = document.querySelector('#recurring-time');
            const addSlotButton = document.querySelector('#recurring-add');
            const specificAvailabilityModal = null;
            const specificTimes = [], scheduleTimes = [], selectedDates = new Set();
            const valueInput = {value:'2026-10-09'}, occupiedScheduleMinutes = [];
            const showSpecificError = message => document.querySelector('#error').textContent = message;
            const showEditorError = showSpecificError;
            const showOverlapWarning = () => 'Overlap';
            const renderSpecificTimes = () => window.specificSlots = [...specificTimes];
            const renderScheduleTimes = () => window.recurringSlots = [...scheduleTimes];
            ${section('    const toScheduleMinute =', '    const occupiedScheduleMinutes =')}
            ${section('    const overlapsSchedule =', '    const overlapWarningElement =')}
            ${section('        specificAddButton.addEventListener', '        specificForm.addEventListener')}
            ${section('        addSlotButton.addEventListener', '        recurringEditor.addEventListener')}
            window.specificSlots = []; window.recurringSlots = [];
        })();` });
        await page.addScriptTag({ path: path.resolve(__dirname, '../../BC_CampusLearn/wwwroot/js/availability-time-picker.js') });
        const popup = page.locator('.availability-wheel-popover:visible');
        const selectTime = async (button, hour, minute, enter = false) => {
            await page.locator(button).click();
            assert.equal(await popup.getByRole('listbox').count(), 2);
            const hours = popup.getByRole('listbox', { name: 'Hour', exact: true });
            const minutes = popup.getByRole('listbox', { name: 'Minute', exact: true });
            await hours.press('Home');
            for (let i = 0; i < hour; i++) await hours.press('ArrowDown');
            await minutes.press('Home');
            for (let i = 0; i < minute; i++) await minutes.press('ArrowDown');
            if (enter) await minutes.press('Enter');
            else await popup.getByRole('button', { name: 'Done' }).click();
            assert.equal(await page.locator('.availability-wheel-popover:visible').count(), 0);
        };
        await selectTime('#recurring-add', 9, 0);
        assert.deepEqual(await page.evaluate(() => window.recurringSlots), ['09:00']);
        assert.equal(await page.locator('#recurring-time').inputValue(), '');
        await selectTime('#recurring-add', 9, 0);
        assert.match(await page.locator('#error').textContent(), /already been added/);
        assert.deepEqual(await page.evaluate(() => window.recurringSlots), ['09:00']);
        await selectTime('#recurring-add', 9, 30);
        assert.equal(await page.locator('#error').textContent(), 'Overlap');
        assert.deepEqual(await page.evaluate(() => window.recurringSlots), ['09:00']);
        await selectTime('#recurring-add', 12, 0, true);
        assert.deepEqual(await page.evaluate(() => window.recurringSlots), ['09:00', '12:00']);
        await selectTime('#specific-add', 0, 0);
        assert.deepEqual(await page.evaluate(() => window.specificSlots), ['00:00']);
        await selectTime('#specific-add', 23, 59);
        assert.deepEqual(await page.evaluate(() => window.specificSlots), ['00:00', '23:59']);
        await page.locator('#specific-add').click();
        await popup.getByRole('button', { name: 'Clear' }).click();
        assert.deepEqual(await page.evaluate(() => window.specificSlots), ['00:00', '23:59']);
    } finally { await browser.close(); }
});
