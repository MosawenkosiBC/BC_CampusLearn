(() => {
    "use strict";
    document.querySelectorAll("[data-edit-interview-open]").forEach(button => button.addEventListener("click", () => {
        button.closest("dialog")?.close();
        document.getElementById(button.dataset.editInterviewOpen)?.showModal();
    }));
    document.querySelectorAll("[data-interview-edit-modal]").forEach(modal => {
        modal.querySelector("[data-edit-interview-close]").addEventListener("click", () => modal.close());
        modal.addEventListener("close", () => document.getElementById(modal.dataset.parentId)?.showModal());
        if (modal.dataset.show === "true") modal.showModal();
    });
    document.querySelectorAll("[data-interview-meeting-form]").forEach(form => {
        const list = form.querySelector("[data-panel-list]");
        const search = form.querySelector("[data-panel-search]");
        const error = form.querySelector("[data-panel-error]");
        const reserved = form.dataset.reserved === "true";
        const editing = form.dataset.editing === "true";
        const picker = form.querySelector("[data-panel-picker]");
        const menu = form.querySelector("[data-panel-options]");
        const options = Array.from(menu.querySelectorAll("[data-panel-option]"));
        let activeOption = null;
        const setActiveOption = option => {
            activeOption?.setAttribute("aria-selected", "false");
            activeOption = option;
            if (option) {
                option.setAttribute("aria-selected", "true");
                search.setAttribute("aria-activedescendant", option.id);
                option.scrollIntoView({ block: "nearest" });
            } else search.removeAttribute("aria-activedescendant");
        };
        const closeOptions = () => {
            menu.hidden = true;
            search.setAttribute("aria-expanded", "false");
            setActiveOption(null);
        };
        const showOptions = () => {
            if (reserved) return;
            setActiveOption(null);
            const query = search.value.trim().toLowerCase();
            options.forEach(option => {
                option.hidden = !`${option.dataset.name} ${option.dataset.email}`.toLowerCase().includes(query) ||
                    Array.from(list.children).some(row => row.dataset.email.toLowerCase() === option.dataset.email.toLowerCase());
            });
            menu.querySelector("[data-panel-empty]").hidden = options.some(option => !option.hidden);
            menu.hidden = false;
            search.setAttribute("aria-expanded", "true");
        };
        const chooseOption = option => {
            search.value = option.dataset.email;
            closeOptions();
            search.focus();
        };
        search.addEventListener("focus", showOptions);
        search.addEventListener("input", () => {
            search.removeAttribute("aria-invalid");
            error.textContent = "";
            showOptions();
        });
        menu.addEventListener("mousedown", event => event.preventDefault());
        menu.addEventListener("click", event => {
            const option = event.target.closest("[data-panel-option]");
            if (option) chooseOption(option);
        });
        picker.addEventListener("focusout", event => {
            if (!picker.contains(event.relatedTarget)) closeOptions();
        });
        const reindex = () => list.querySelectorAll("[data-panel-member]").forEach((row, i) => {
            row.querySelector("[data-panel-email]").name = `Meeting.Panel[${i}].Email`;
            row.querySelector("[data-panel-name]").name = `Meeting.Panel[${i}].Name`;
            row.querySelector("[data-panel-optional]").name = `Meeting.Panel[${i}].Optional`;
        });
        const node = (tag, className, text) => {
            const element = document.createElement(tag);
            if (className) element.className = className;
            if (text) element.textContent = text;
            return element;
        };
        let attendanceId = 0;
        const styleAttendance = select => {
            const wrapper = node("div", "interview-attendance");
            const trigger = node("button", "interview-attendance-trigger");
            trigger.type = "button";
            trigger.setAttribute("aria-haspopup", "listbox");
            trigger.setAttribute("aria-expanded", "false");
            const popup = node("div", "interview-attendance-options");
            popup.id = `${form.id}-attendance-${attendanceId++}`;
            popup.setAttribute("popover", "auto");
            popup.setAttribute("role", "listbox");
            popup.setAttribute("aria-label", select.getAttribute("aria-label"));
            trigger.setAttribute("aria-controls", popup.id);
            trigger.setAttribute("popovertarget", popup.id);
            const choices = Array.from(select.options).map(option => {
                const button = node("button", null, option.textContent);
                button.type = "button";
                button.tabIndex = -1;
                button.setAttribute("role", "option");
                button.dataset.value = option.value;
                button.addEventListener("click", () => {
                    select.value = option.value;
                    select.dispatchEvent(new Event("change", { bubbles: true }));
                    popup.hidePopover();
                    trigger.focus();
                });
                popup.append(button);
                return button;
            });
            const sync = () => {
                trigger.textContent = select.selectedOptions[0].textContent;
                trigger.setAttribute("aria-label", `${select.getAttribute("aria-label")}: ${trigger.textContent}`);
                choices.forEach(choice => choice.setAttribute("aria-selected", String(choice.dataset.value === select.value)));
            };
            const positionPopup = () => {
                const rect = trigger.getBoundingClientRect();
                popup.style.width = `${Math.max(rect.width, 120)}px`;
                popup.style.left = `${Math.max(8, Math.min(rect.left, window.innerWidth - popup.offsetWidth - 8))}px`;
                popup.style.top = `${rect.bottom + popup.offsetHeight + 6 > window.innerHeight ? Math.max(8, rect.top - popup.offsetHeight - 6) : rect.bottom + 6}px`;
            };
            const open = () => { popup.showPopover(); positionPopup(); };
            trigger.addEventListener("keydown", event => {
                if (event.key === "ArrowDown" || event.key === "ArrowUp") { event.preventDefault(); open(); }
            });
            popup.addEventListener("keydown", event => {
                const index = choices.indexOf(document.activeElement);
                if (["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) {
                    event.preventDefault();
                    const next = event.key === "Home" ? 0 : event.key === "End" ? choices.length - 1 :
                        (index + (event.key === "ArrowDown" ? 1 : -1) + choices.length) % choices.length;
                    choices[next].focus();
                } else if (event.key === "Escape" || event.key === "Tab") {
                    if (event.key === "Escape") { event.preventDefault(); event.stopPropagation(); }
                    popup.hidePopover(); trigger.focus();
                }
            });
            popup.addEventListener("toggle", event => {
                trigger.setAttribute("aria-expanded", String(event.newState === "open"));
                if (event.newState === "open") {
                    positionPopup();
                    choices.find(choice => choice.dataset.value === select.value)?.focus();
                }
            });
            select.addEventListener("change", sync);
            select.before(wrapper);
            wrapper.append(select, trigger, popup);
            select.hidden = true;
            sync();
        };
        list.querySelectorAll("[data-panel-optional]").forEach(styleAttendance);
        const closeAttendance = () => list.querySelectorAll(".interview-attendance-options:popover-open").forEach(popup => popup.hidePopover());
        window.addEventListener("resize", closeAttendance);
        form.closest("dialog")?.addEventListener("scroll", closeAttendance);
        form.closest(".shortlist-meeting-columns")?.addEventListener("scroll", closeAttendance);
        list.addEventListener("scroll", closeAttendance);
        const addMember = () => {
            if (reserved) return;
            error.textContent = "";
            const value = search.value.trim();
            const person = options.find(o => o.dataset.email.toLowerCase() === value.toLowerCase()) ||
                options.find(o => o.dataset.name?.toLowerCase() === value.toLowerCase());
            if (!person) {
                error.textContent = "Select an Admin from the list.";
                search.setAttribute("aria-invalid", "true");
                search.focus();
                return;
            }
            const email = person.dataset.email;
            if (Array.from(list.children).some(row => row.dataset.email.toLowerCase() === email.toLowerCase())) {
                error.textContent = "This person is already on the panel.";
                return;
            }
            if (list.children.length >= 20) { error.textContent = "You can add up to 20 panel members."; return; }
            const name = person?.dataset.name || email;
            const row = node("li");
            row.dataset.panelMember = "";
            row.dataset.email = email;
            row.append(node("span", "interview-panel-avatar", name.slice(0, 1).toUpperCase()));
            const details = node("span", "interview-panel-person");
            const heading = node("strong", null, name);
            if (email.toLowerCase() === form.dataset.adminEmail?.toLowerCase())
                heading.append(node("span", "interview-you", "You"));
            details.append(heading, node("small", null, email));
            row.append(details);
            for (const [key, value] of [["panelEmail", email], ["panelName", name]]) {
                const input = node("input"); input.type = "hidden"; input.dataset[key] = ""; input.value = value; row.append(input);
            }
            const attendance = node("select");
            attendance.dataset.panelOptional = "";
            attendance.setAttribute("aria-label", `Attendance for ${name}`);
            for (const [value, label] of [["false", "Required"], ["true", "Optional"]]) {
                const option = node("option", null, label); option.value = value; attendance.append(option);
            }
            const remove = node("button", null, "×");
            remove.type = "button"; remove.dataset.panelRemove = "";
            remove.setAttribute("aria-label", `Remove ${name} from panel`);
            row.append(attendance, remove); list.append(row); reindex();
            styleAttendance(attendance);
            search.value = ""; search.removeAttribute("aria-invalid"); search.focus(); closeOptions();
        };
        form.querySelector("[data-panel-add]").addEventListener("click", addMember);
        search.addEventListener("keydown", event => {
            if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                event.preventDefault();
                if (menu.hidden) showOptions();
                const visible = options.filter(option => !option.hidden);
                const index = visible.indexOf(activeOption);
                const next = event.key === "ArrowDown" ? (index + 1) % visible.length : (index < 0 ? visible.length - 1 : (index - 1 + visible.length) % visible.length);
                setActiveOption(visible[next] || null);
            } else if (event.key === "Enter") {
                event.preventDefault();
                if (!menu.hidden && activeOption) chooseOption(activeOption);
                else addMember();
            } else if (event.key === "Escape" && !menu.hidden) {
                event.preventDefault(); event.stopPropagation(); closeOptions();
            } else if (event.key === "Tab") closeOptions();
        });
        list.addEventListener("click", event => {
            const button = event.target.closest("[data-panel-remove]");
            if (!button || reserved) return;
            button.closest("[data-panel-member]").remove(); reindex();
            error.textContent = list.children.length ? "Panel member removed." : "Add at least one interview panel member.";
        });
        const date = form.querySelector('input[type="date"]');
        date.min = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString().slice(0, 10);
        let submitting = false;
        form.addEventListener("submit", event => {
            if (submitting) { event.preventDefault(); return; }
            if (!list.querySelector("[data-panel-member]")) {
                event.preventDefault(); error.textContent = "Add at least one interview panel member.";
                search.setAttribute("aria-invalid", "true"); search.focus(); return;
            }
            if (!reserved && search.value.trim()) {
                event.preventDefault(); error.textContent = "Click Add to include this panel member, or clear the search field."; search.focus(); return;
            }
            reindex(); submitting = true;
            form.setAttribute("aria-busy", "true");
            const button = document.querySelector(`[data-create-interview][form="${form.id}"]`);
            if (button) { button.disabled = true; button.textContent = editing ? "Updating meeting…" : "Creating meeting…"; }
        });
        window.addEventListener("pageshow", () => {
            submitting = false; form.removeAttribute("aria-busy");
            const button = document.querySelector(`[data-create-interview][form="${form.id}"]`);
            if (button) { button.disabled = false; button.textContent = editing ? "Save & send update" : reserved ? "Retry meeting & move to interview" : "Create meeting & move to interview"; }
        });
    });
})();
