const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const readScript = file => fs.readFileSync(path.join(__dirname,
    '../../BC_CampusLearn/wwwroot/js', file), 'utf8');

function setup({ page = '', state = { otherState: true }, mobile = false, optIn = true } = {}) {
    const listeners = {};
    const rows = Array.from({ length: 40 }, (_, i) => ({
        id: `tutor-${i + 1}`, dataset: {}, hidden: false,
        scrollIntoView() { this.scrolled = true; }
    }));
    const links = rows.map(row => ({
        href: `https://campus.test/Tutors/Details/${row.id.slice(6)}`,
        closest() { return row; }
    }));
    const element = () => ({
        dataset: {}, classList: { add() {} },
        setAttribute() {}, addEventListener(name, handler) { this[name] = handler; }
    });
    const previous = element(), next = element(), status = element();
    const pages = { children: [], replaceChildren() { this.children = []; },
        append(child) { this.children.push(child); } };
    const grid = {
        dataset: optIn ? { paginationPageParameter: 'tutorPage' } : {},
        querySelectorAll(selector) { return selector === '.tutor-card-link' ? links : rows; },
        addEventListener(name, handler) { listeners[name] = handler; },
        scrollIntoView() {}
    };
    const pagination = {
        dataset: { paginationTarget: 'tutor-results-grid',
            paginationMobilePageSize: '8', paginationDefaultPageSize: '12' },
        querySelector(selector) { return {
            '[data-pagination-previous]': previous, '[data-pagination-next]': next,
            '[data-pagination-pages]': pages, '[data-pagination-status]': status
        }[selector]; }
    };
    const window = {
        location: new URL(`https://campus.test/Tutors?Campus=Pretoria${page}`),
        history: { state, replaceState(value, _, url) {
            this.state = value;
            if (url) window.location = new URL(url, window.location);
        } },
        scrollX: 0, scrollY: 740,
        scrollTo(value) { this.restoredScroll = value; },
        matchMedia() { return { matches: mobile, addEventListener() {} }; },
        addEventListener(name, handler) { listeners[name] = handler; }
    };
    class CustomEvent { constructor(detail) { this.detail = detail; } }
    const context = {
        URL, CustomEvent, window,
        document: {
            querySelectorAll() { return [pagination]; },
            querySelector() { return null; },
            getElementById(id) { return id === 'tutor-results-grid' ? grid : rows.find(row => row.id === id); },
            createElement: element
        }
    };
    vm.runInNewContext(readScript('components/pagination.js'), context);
    vm.runInNewContext(readScript('tutor-directory-navigation.js'), context);
    return { window, rows, links, listeners, status, pages, CustomEvent };
}

for (const mobile of [false, true]) {
    for (const page of [2, 3]) {
        test(`return restores page ${page} and clicked position (${mobile ? 'mobile' : 'desktop'})`, () => {
            const ui = setup({ mobile });
            ui.pages.children.find(button => button.textContent === String(page)).click();
            const firstIndex = (page - 1) * (mobile ? 8 : 12);
            assert.equal(ui.rows[firstIndex].hidden, false);
            ui.listeners.click({ target: { closest() { return ui.links[firstIndex]; } } });
            const returnUrl = new URL(ui.links[firstIndex].href).searchParams.get('returnUrl');
            assert.match(returnUrl, new RegExp(`tutorPage=${page}`));
            assert.match(returnUrl, /Campus=Pretoria/);
            assert.ok(returnUrl.endsWith(`#tutor-${firstIndex + 1}`));
            const returned = setup({ mobile, page: `&tutorPage=${page}`, state: ui.window.history.state });
            returned.listeners.pageshow({ persisted: false });
            assert.match(returned.status.textContent, new RegExp(`Page ${page} of`));
            assert.equal(returned.rows[firstIndex].hidden, false);
            assert.equal(returned.window.restoredScroll.top, 740);
            assert.equal(returned.window.history.state.otherState, true);
            returned.window.restoredScroll = null;
            returned.listeners.pageshow({ persisted: true });
            assert.equal(returned.window.restoredScroll, null);
        });
    }
}

test('filter refresh resets the page; invalid and out-of-range pages are safe', () => {
    for (const page of ['oops', '-2', '2.5', 'Infinity']) {
        assert.match(setup({ page: `&tutorPage=${page}` }).status.textContent, /Page 1 of/);
    }
    const ui = setup({ page: '&tutorPage=999' });
    assert.match(ui.status.textContent, /Page 4 of 4/);
    ui.rows.slice(2).forEach(row => { row.dataset.paginationExcluded = 'true'; });
    ui.listeners['campus:pagination-refresh'](new ui.CustomEvent({ resetPage: true }));
    assert.match(ui.status.textContent, /Page 1 of 1/);
    assert.equal(ui.window.location.searchParams.has('tutorPage'), false);
});

test('other pagination components keep their existing behavior', () => {
    const ui = setup({ page: '&tutorPage=3', optIn: false });
    assert.match(ui.status.textContent, /Page 1 of/);
    assert.equal(ui.window.location.searchParams.get('tutorPage'), '3');
});

test('the Tutors breadcrumb accepts only a return to the same directory', () => {
    for (const destination of ['/Tutors?Campus=Pretoria&tutorPage=3#tutor-26',
        'https://evil.test/Tutors', '/Administrator', 'javascript:alert(1)']) {
        const link = { href: 'https://campus.test/Tutors' };
        const window = { location: new URL(`https://campus.test/Tutors/Details/26?returnUrl=${encodeURIComponent(destination)}`) };
        vm.runInNewContext(readScript('tutor-directory-navigation.js'), {
            URL, window, document: { getElementById() { return null; }, querySelector() { return link; } }
        });
        assert.equal(link.href, destination.startsWith('/Tutors?')
            ? `https://campus.test${destination}` : 'https://campus.test/Tutors');
    }
});
