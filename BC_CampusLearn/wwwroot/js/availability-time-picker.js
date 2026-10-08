(() => {
    const parts = value => {
        const match = /^(\d{2}):(\d{2})(?::00(?:\.0+)?)?$/.exec(value ?? "");
        if (!match || Number(match[1]) > 23 || Number(match[2]) > 59) return null;
        return { hour: Number(match[1]), minute: Number(match[2]) };
    };
    const valueOf = ({ hour, minute }) => {
        if (!Number.isInteger(hour) || hour < 0 || hour > 23 ||
            !Number.isInteger(minute) || minute < 0 || minute > 59) return "";
        return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
    };
    const labelOf = value => {
        const time = parts(value);
        return time ? valueOf(time) : "Choose time";
    };
    if (typeof module !== "undefined") module.exports = { parts, valueOf, labelOf };
    if (typeof document === "undefined") return;

    let closeActive = null;
    document.querySelectorAll(".manage-availability-page input[type='time'], #specific-availability-modal input[type='time'], .specific-availability-modal input[type='time'], .availability-edit-modal input[type='time']")
        .forEach((input, number) => {
            const field = document.createElement("span");
            field.className = "availability-wheel-field";
            const trigger = document.createElement("button");
            trigger.type = "button";
            trigger.className = "availability-wheel-trigger";
            trigger.setAttribute("aria-haspopup", "dialog");
            trigger.setAttribute("aria-expanded", "false");
            const label = input.labels?.[0]?.textContent.trim() || "Time";
            const sync = () => {
                trigger.textContent = labelOf(input.value);
                trigger.setAttribute("aria-label", `${label}: ${labelOf(input.value)}`);
                trigger.disabled = input.disabled;
            };
            input.before(field);
            field.append(input, trigger);
            input.type = "hidden";
            if (parts(input.value)) input.value = valueOf(parts(input.value));
            sync();
            input.addEventListener("change", sync);
            input.form?.addEventListener("reset", () => setTimeout(sync, 0));

            const panel = document.createElement("div");
            panel.id = `availability-wheel-${number}`;
            panel.className = "availability-wheel-popover";
            panel.setAttribute("role", "dialog");
            panel.setAttribute("aria-label", label);
            panel.hidden = true;
            trigger.setAttribute("aria-controls", panel.id);
            // Keep the popup within Bootstrap's focus boundary when editing in a modal.
            (input.closest(".modal") || document.body).append(panel);
            const wheels = document.createElement("div");
            wheels.className = "availability-wheel-columns";
            const highlight = document.createElement("span");
            highlight.className = "availability-wheel-highlight";
            highlight.setAttribute("aria-hidden", "true");
            wheels.append(highlight);
            panel.append(wheels);
            let selected = { hour: 9, minute: 0 };
            let opening = false;
            const columns = [];

            const column = (key, values, title) => {
                const offset = values.length;
                const wheelValues = [...values, ...values, ...values];
                const element = document.createElement("div");
                element.className = "availability-wheel-column";
                element.setAttribute("role", "listbox");
                element.setAttribute("aria-label", title);
                element.tabIndex = 0;
                const options = wheelValues.map((value, index) => {
                    const option = document.createElement("div");
                    option.id = `${panel.id}-${key}-${index}`;
                    option.className = "availability-wheel-option";
                    option.setAttribute("role", "option");
                    option.textContent = String(value).padStart(2, "0");
                    option.addEventListener("click", () => select(index));
                    element.append(option);
                    return option;
                });
                const paint = index => {
                    selected[key] = wheelValues[index];
                    options.forEach((option, position) => {
                        const distance = Math.abs(index - position);
                        option.style.opacity = distance === 0 ? "1" : distance === 1 ? "0.45" : "0.2";
                        option.classList.toggle("is-selected", index === position);
                        option.setAttribute("aria-selected", String(index === position));
                    });
                    element.setAttribute("aria-activedescendant", options[index].id);
                };
                const select = index => {
                    index = Math.max(0, Math.min(wheelValues.length - 1, index));
                    paint(index);
                    element.scrollTop = index * 32;
                };
                element.addEventListener("scroll", () => {
                    if (opening || panel.hidden) return;
                    const index = Math.max(0, Math.min(wheelValues.length - 1, Math.round(element.scrollTop / 32)));
                    paint(index);
                    if (offset && index < offset / 2) select(index + offset);
                    else if (offset && index >= offset * 2.5) select(index - offset);
                });
                element.addEventListener("keydown", event => {
                    const index = Math.round(element.scrollTop / 32);
                    const next = { ArrowUp: index - 1, ArrowDown: index + 1,
                        Home: offset, End: offset + values.length - 1 }[event.key];
                    if (next !== undefined) { event.preventDefault(); select(next); }
                });
                wheels.append(element);
                columns.push({ element, select: value => select(offset + values.indexOf(value)), key });
            };
            column("hour", Array.from({ length: 24 }, (_, i) => i), "Hour");
            column("minute", Array.from({ length: 60 }, (_, i) => i), "Minute");

            const actions = document.createElement("div");
            actions.className = "availability-wheel-actions";
            const clear = document.createElement("button");
            clear.type = "button";
            clear.textContent = "Clear";
            const done = document.createElement("button");
            done.type = "button";
            done.textContent = "Done";
            actions.append(clear, done);
            panel.append(actions);
            const close = (restoreFocus = false) => {
                panel.hidden = true;
                trigger.setAttribute("aria-expanded", "false");
                if (closeActive === close) closeActive = null;
                if (restoreFocus) trigger.focus();
            };
            const commit = value => {
                input.value = value;
                input.dispatchEvent(new Event("input", { bubbles: true }));
                input.dispatchEvent(new Event("change", { bubbles: true }));
                close(true);
                if (value) input.dispatchEvent(new Event("availability-time-confirm", { bubbles: true }));
            };
            clear.addEventListener("click", () => commit(""));
            done.addEventListener("click", () => commit(valueOf(selected)));
            panel.addEventListener("keydown", event => {
                if (event.key === "Escape") { event.preventDefault(); event.stopPropagation(); close(true); }
                if (event.key === "Enter" && event.target.classList.contains("availability-wheel-column")) {
                    event.preventDefault(); commit(valueOf(selected));
                }
            });
            const position = () => {
                if (panel.hidden) return;
                const rect = trigger.getBoundingClientRect();
                panel.style.left = `${Math.max(8, Math.min(rect.left, window.innerWidth - panel.offsetWidth - 8))}px`;
                const below = rect.bottom + 8;
                panel.style.top = `${below + panel.offsetHeight <= window.innerHeight - 8
                    ? below : Math.max(8, rect.top - panel.offsetHeight - 8)}px`;
            };
            const open = () => {
                closeActive?.();
                selected = parts(input.value) || { hour: 9, minute: 0 };
                opening = true;
                panel.hidden = false;
                trigger.setAttribute("aria-expanded", "true");
                closeActive = close;
                columns.forEach(item => item.select(selected[item.key]));
                position();
                columns[0].element.focus({ preventScroll: true });
                requestAnimationFrame(() => { opening = false; });
            };
            trigger.addEventListener("click", () => {
                if (!panel.hidden) close();
                else open();
            });
            input.addEventListener("availability-time-open", open);
            document.addEventListener("pointerdown", event => {
                if (!field.contains(event.target) && !panel.contains(event.target)) close();
            });
            panel.addEventListener("focusout", event => {
                // Safari may send a null relatedTarget when tapping a button.
                // Outside taps and Escape still dismiss the popup.
                if (event.relatedTarget && !panel.contains(event.relatedTarget) && event.relatedTarget !== trigger) close();
            });
            input.closest(".modal")?.addEventListener("hide.bs.modal", () => close());
            window.addEventListener("resize", position);
            document.addEventListener("scroll", event => {
                if (!panel.contains(event.target)) position();
            }, true);
        });
})();
