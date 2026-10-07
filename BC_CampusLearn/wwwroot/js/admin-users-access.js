(() => {
    const roleModal = document.getElementById("access-role-modal");
    let selectedActionUser;
    const showActionChoices = () => {
        roleModal.querySelector("[data-access-role-form]").hidden = true;
        roleModal.querySelector("[data-access-role-choices]").hidden = false;
        roleModal.querySelector("#access-role-title").textContent = "Manage access";
    };
    roleModal?.addEventListener("show.bs.modal", event => {
        const trigger = event.relatedTarget;
        if (!trigger) return;
        selectedActionUser = trigger.dataset;
        showActionChoices();
        roleModal.querySelector("[data-access-role-name]").textContent = trigger.dataset.userName;
        const promoteButton = roleModal.querySelector('[data-access-choose="promote"]');
        promoteButton.hidden = Number(trigger.dataset.userRole) === 3;
        promoteButton.disabled = Number(trigger.dataset.userRole) >= 5;
        const tutorHead = Number(trigger.dataset.userRole) === 3;
        roleModal.querySelector("[data-access-role-back]").textContent = tutorHead ? "Cancel" : "Back";
        if (tutorHead) chooseAction("demote");
    });
    const chooseAction = action => {
        if (!selectedActionUser) return;
        const promote = action === "promote";
        if (promote && (Number(selectedActionUser.userRole) === 3 || Number(selectedActionUser.userRole) >= 5)) return;
        const form = roleModal.querySelector("[data-access-role-form]");
        form.reset();
        form.hidden = false;
        roleModal.querySelector("[data-access-role-choices]").hidden = true;
        form.action = promote ? form.dataset.promoteUrl : form.dataset.demoteUrl;
        form.querySelector("[data-access-role-user]").value = selectedActionUser.userId;
        roleModal.querySelector("#access-role-title").textContent = promote ? "Promote user" : "Demote user";
        roleModal.querySelector("[data-access-role-description]").textContent = promote
            ? `Choose a higher role for ${selectedActionUser.userName}.`
            : `${selectedActionUser.userName} will become ${selectedActionUser.demotionRole}. Their access will change to match this role.`;
        const select = form.querySelector("#access-promotion-role");
        form.querySelector("[data-access-promotion-field]").hidden = !promote;
        select.disabled = !promote;
        [...select.options].forEach(option => {
            option.disabled = Number(option.dataset.rank) <= Number(selectedActionUser.userRole);
            option.hidden = option.disabled;
        });
        select.value = [...select.options].find(option => !option.disabled)?.value || "";
        const submit = form.querySelector("[data-access-role-submit]");
        submit.textContent = promote ? "Promote" : "Demote";
        submit.className = promote
            ? "settings-primary-button settings-access-confirm--promote"
            : "settings-danger-button settings-access-confirm--demote";
        (promote ? select : submit).focus();
    };
    roleModal?.querySelectorAll("[data-access-choose]").forEach(button => {
        button.addEventListener("click", () => chooseAction(button.dataset.accessChoose));
    });
    roleModal?.querySelector("[data-access-role-back]").addEventListener("click", () => {
        if (Number(selectedActionUser?.userRole) === 3) {
            window.bootstrap.Modal.getOrCreateInstance(roleModal).hide();
            return;
        }
        showActionChoices();
        roleModal.querySelector('[data-access-choose="demote"]').focus();
    });
    roleModal?.addEventListener("shown.bs.modal", () => {
        if (!roleModal.querySelector("[data-access-role-form]").hidden) {
            roleModal.querySelector("[data-access-role-submit]").focus();
            return;
        }
        const promote = roleModal.querySelector('[data-access-choose="promote"]');
        (promote.hidden || promote.disabled ? roleModal.querySelector('[data-access-choose="demote"]') : promote).focus();
    });
    const modal = document.getElementById("grant-access-modal");
    if (!modal) return;
    const input = modal.querySelector("[data-access-search]");
    const userId = modal.querySelector("[data-access-user-id]");
    const results = modal.querySelector("#access-search-results");
    const status = modal.querySelector("#access-search-status");
    const currentRole = modal.querySelector("[data-access-current-role]");
    const removeButton = modal.querySelector("[data-access-remove]");
    const saveButton = modal.querySelector("[data-access-save]");
    let timer;
    let request;
    let version = 0;

    const clearResults = () => {
        results.replaceChildren();
        results.hidden = true;
    };
    const searchUsers = async (term, searchVersion) => {
        request = new AbortController();
        status.textContent = "Searching users…";
        try {
            const url = new URL(input.dataset.searchUrl, window.location.href);
            url.searchParams.set("term", term);
            const response = await fetch(url, {
                signal: request.signal,
                headers: { Accept: "application/json" }
            });
            if (!response.ok) throw new Error("Search failed");
            const users = await response.json();
            if (searchVersion !== version) return;
            clearResults();
            status.textContent = users.length
                ? `${users.length} matching user${users.length === 1 ? "" : "s"}. Select a user below.`
                : "No matching users found. Try another student number or email address.";
            users.forEach(user => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "settings-access-search-result";
                const name = document.createElement("strong");
                name.textContent = user.displayName;
                const details = document.createElement("span");
                details.textContent = [user.personnelNumber, user.email, user.role].filter(Boolean).join(" · ");
                button.append(name, details);
                button.addEventListener("click", () => {
                    version++;
                    userId.value = user.userId;
                    input.value = user.personnelNumber || user.email;
                    clearResults();
                    status.textContent = `Selected: ${user.displayName} (${details.textContent}).`;
                    currentRole.hidden = false;
                    currentRole.textContent = `${user.displayName} — Current role: ${user.role}. ` +
                        (user.hasAdministrativeAccess
                            ? `Already has administrative access${user.isActive ? "." : " (inactive)."}`
                            : "No administrative access assigned.");
                    removeButton.hidden = !user.hasAdministrativeAccess;
                    saveButton.textContent = user.hasAdministrativeAccess ? "Update access" : "Grant access";
                    saveButton.hidden = user.role === "Head of Tutors";
                    modal.querySelector("[data-access-grant-role]").hidden = saveButton.hidden;
                    const role = modal.querySelector("#Input_Role");
                    if (user.hasAdministrativeAccess) {
                        role.value = [...role.options].find(option => option.text === user.role)?.value || "Admin";
                    } else {
                        role.value = "Admin";
                    }
                    (saveButton.hidden ? removeButton : role).focus();
                });
                results.append(button);
            });
            results.hidden = users.length === 0;
        } catch (error) {
            if (error.name === "AbortError" || searchVersion !== version) return;
            clearResults();
            status.textContent = "Unable to search users. Please try again.";
        }
    };
    input.addEventListener("input", () => {
        clearTimeout(timer);
        request?.abort();
        const searchVersion = ++version;
        userId.value = "";
        currentRole.hidden = true;
        removeButton.hidden = true;
        saveButton.textContent = "Grant access";
        saveButton.hidden = false;
        modal.querySelector("[data-access-grant-role]").hidden = false;
        clearResults();
        const term = input.value.trim();
        if (term.length < 2) {
            status.textContent = "Type at least two characters to find a user.";
            return;
        }
        status.textContent = "Searching users…";
        timer = setTimeout(() => searchUsers(term, searchVersion), 250);
    });
    modal.querySelector("[data-access-grant-form]").addEventListener("submit", event => {
        if (userId.value) return;
        event.preventDefault();
        status.textContent = "Select a matching user before changing access.";
        input.focus();
    });
    modal.addEventListener("shown.bs.modal", () => {
        input.focus();
        if (input.value.trim() && !userId.value) input.dispatchEvent(new Event("input"));
    });
    modal.addEventListener("hidden.bs.modal", () => {
        clearTimeout(timer);
        request?.abort();
        version++;
        clearResults();
    });
})();
