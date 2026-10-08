document.addEventListener("DOMContentLoaded", () => {
    const profileTabs = document.querySelector(".tutor-profile-nav");
    const activeProfileTab = profileTabs?.querySelector(
        ".tutor-profile-nav-link.is-active");
    const mobileProfileTabs = window.matchMedia("(max-width: 767.98px)");
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const accountTabScrollKey = "tutor-account-tab-scroll-position";

    try {
        const savedScrollPosition = JSON.parse(
            sessionStorage.getItem(accountTabScrollKey) ?? "null");
        const currentPath = window.location.pathname.toLocaleLowerCase();

        if (savedScrollPosition?.path === currentPath &&
            Number.isFinite(savedScrollPosition.scrollY)) {
            sessionStorage.removeItem(accountTabScrollKey);
            if ("scrollRestoration" in history) {
                history.scrollRestoration = "manual";
            }
            requestAnimationFrame(() => requestAnimationFrame(() => {
                window.scrollTo({
                    top: savedScrollPosition.scrollY,
                    behavior: "auto"
                });
            }));
        }
    } catch {
        // Storage can be unavailable in privacy-restricted browsing modes.
    }

    profileTabs?.querySelectorAll(".tutor-profile-nav-link").forEach((link) => {
        link.addEventListener("click", (event) => {
            if (event.button !== 0 || event.metaKey || event.ctrlKey ||
                event.shiftKey || event.altKey) {
                return;
            }

            try {
                const targetPath = new URL(link.href, window.location.href)
                    .pathname.toLocaleLowerCase();
                sessionStorage.setItem(accountTabScrollKey, JSON.stringify({
                    path: targetPath,
                    scrollY: window.scrollY
                }));
            } catch {
                // Tab navigation should still work when storage is unavailable.
            }
        });
    });

    if (profileTabs && activeProfileTab && mobileProfileTabs.matches) {
        const profileTabLinks = Array.from(
            profileTabs.querySelectorAll(".tutor-profile-nav-link"));
        const activeTabIndex = profileTabLinks.indexOf(activeProfileTab);
        const indicator = document.createElement("span");
        let isNavigating = false;

        indicator.className = "tutor-profile-tab-indicator";
        indicator.setAttribute("aria-hidden", "true");
        profileTabs.append(indicator);
        profileTabs.classList.add("has-animated-indicator");

        const positionIndicator = (link, animate = true) => {
            if (!link) {
                return;
            }

            indicator.classList.toggle("is-ready", animate);
            indicator.style.width = `${link.offsetWidth}px`;
            indicator.style.transform = `translateX(${link.offsetLeft}px)`;
        };

        profileTabs.scrollLeft = Math.max(
            0,
            activeProfileTab.offsetLeft -
                ((profileTabs.clientWidth - activeProfileTab.offsetWidth) / 2));
        positionIndicator(activeProfileTab, false);
        requestAnimationFrame(() => indicator.classList.add("is-ready"));

        profileTabLinks.forEach((link, targetTabIndex) => {
            link.addEventListener("click", (event) => {
                if (isNavigating || targetTabIndex === activeTabIndex ||
                    event.button !== 0 || event.metaKey || event.ctrlKey ||
                    event.shiftKey || event.altKey || !mobileProfileTabs.matches) {
                    return;
                }

                if (reducedMotion.matches) {
                    return;
                }

                event.preventDefault();
                isNavigating = true;
                activeProfileTab.classList.remove("is-active");
                activeProfileTab.removeAttribute("aria-current");
                link.classList.add("is-active", "is-switching-to");
                link.setAttribute("aria-current", "page");
                positionIndicator(link);

                window.setTimeout(() => window.location.assign(link.href), 190);
            });
        });
    }

    document.querySelectorAll(".modal[data-open-on-load='true']").forEach((element) => {
        if (window.bootstrap) {
            window.bootstrap.Modal.getOrCreateInstance(element).show();
        }
    });

    const imageForm = document.querySelector("[data-profile-image-form]");
    const imageInput = imageForm?.querySelector("[data-profile-image-input]");
    const cropperElement = document.querySelector("[data-profile-image-cropper]");
    const cropCanvas = cropperElement?.querySelector("[data-profile-crop-canvas]");
    const cropZoom = cropperElement?.querySelector("[data-profile-crop-zoom]");
    const cropSave = cropperElement?.querySelector("[data-profile-crop-save]");
    const cropError = cropperElement?.querySelector("[data-profile-crop-error]");

    if (imageForm && imageInput && cropperElement && cropCanvas && cropZoom &&
        cropSave && window.bootstrap) {
        // Bootstrap appends its backdrop directly to <body>. Keep the modal
        // there too so page-level stacking contexts cannot cover its controls.
        document.body.append(cropperElement);
        const cropContext = cropCanvas.getContext("2d");
        const cropModal = window.bootstrap.Modal.getOrCreateInstance(cropperElement);
        const cropState = {
            image: null,
            baseScale: 1,
            zoom: 1,
            centerX: cropCanvas.width / 2,
            centerY: cropCanvas.height / 2,
            activePointerId: null,
            pointerX: 0,
            pointerY: 0,
            dragging: false,
            submitting: false
        };
        const acceptedImageTypes = new Set([
            "image/jpeg",
            "image/png",
            "image/webp"
        ]);
        const maximumImageSize = 5 * 1024 * 1024;

        const constrainPosition = () => {
            if (!cropState.image) {
                return;
            }

            const width = cropState.image.naturalWidth *
                cropState.baseScale * cropState.zoom;
            const height = cropState.image.naturalHeight *
                cropState.baseScale * cropState.zoom;
            cropState.centerX = Math.min(
                width / 2,
                Math.max(cropCanvas.width - (width / 2), cropState.centerX));
            cropState.centerY = Math.min(
                height / 2,
                Math.max(cropCanvas.height - (height / 2), cropState.centerY));
        };

        const drawCrop = () => {
            if (!cropContext || !cropState.image) {
                return;
            }

            constrainPosition();
            const width = cropState.image.naturalWidth *
                cropState.baseScale * cropState.zoom;
            const height = cropState.image.naturalHeight *
                cropState.baseScale * cropState.zoom;

            cropContext.clearRect(0, 0, cropCanvas.width, cropCanvas.height);
            cropContext.drawImage(
                cropState.image,
                cropState.centerX - (width / 2),
                cropState.centerY - (height / 2),
                width,
                height);
        };

        const showCropError = (message) => {
            if (!cropError) {
                return;
            }

            cropError.textContent = message;
            cropError.hidden = !message;
        };

        imageInput.addEventListener("change", () => {
            const file = imageInput.files?.[0];
            if (!file) {
                return;
            }

            if (!acceptedImageTypes.has(file.type.toLowerCase())) {
                imageInput.value = "";
                showCropError("Choose a JPG, PNG, or WebP image.");
                cropModal.show();
                return;
            }

            if (file.size > maximumImageSize) {
                imageInput.value = "";
                showCropError("The profile image must be smaller than 5 MB.");
                cropModal.show();
                return;
            }

            const imageUrl = URL.createObjectURL(file);
            const selectedImage = new Image();

            selectedImage.addEventListener("load", () => {
                URL.revokeObjectURL(imageUrl);
                cropState.image = selectedImage;
                cropState.baseScale = Math.max(
                    cropCanvas.width / selectedImage.naturalWidth,
                    cropCanvas.height / selectedImage.naturalHeight);
                cropState.zoom = 1;
                cropState.centerX = cropCanvas.width / 2;
                cropState.centerY = cropCanvas.height / 2;
                cropState.submitting = false;
                cropZoom.value = "1";
                cropSave.disabled = false;
                showCropError("");
                drawCrop();
                cropModal.show();
            }, { once: true });

            selectedImage.addEventListener("error", () => {
                URL.revokeObjectURL(imageUrl);
                imageInput.value = "";
                showCropError("We couldn't read that image. Please try another one.");
                cropModal.show();
            }, { once: true });
            selectedImage.src = imageUrl;
        });

        cropZoom.addEventListener("input", () => {
            const nextZoom = Number.parseFloat(cropZoom.value);
            cropState.zoom = Number.isFinite(nextZoom) ? nextZoom : 1;
            drawCrop();
        });

        cropCanvas.addEventListener("pointerdown", (event) => {
            if (!cropState.image || cropState.dragging ||
                (event.pointerType === "mouse" && event.button !== 0)) {
                return;
            }

            event.preventDefault();
            cropState.dragging = true;
            cropState.activePointerId = event.pointerId;
            cropState.pointerX = event.clientX;
            cropState.pointerY = event.clientY;
            cropCanvas.setPointerCapture(event.pointerId);
            cropCanvas.classList.add("is-dragging");
        });

        cropCanvas.addEventListener("pointermove", (event) => {
            if (!cropState.dragging ||
                event.pointerId !== cropState.activePointerId) {
                return;
            }

            event.preventDefault();
            const bounds = cropCanvas.getBoundingClientRect();
            if (bounds.width === 0 || bounds.height === 0) {
                return;
            }

            const scaleX = cropCanvas.width / bounds.width;
            const scaleY = cropCanvas.height / bounds.height;
            cropState.centerX += (event.clientX - cropState.pointerX) * scaleX;
            cropState.centerY += (event.clientY - cropState.pointerY) * scaleY;
            cropState.pointerX = event.clientX;
            cropState.pointerY = event.clientY;
            drawCrop();
        });

        const finishDragging = (event) => {
            if (event && cropState.activePointerId !== null &&
                event.pointerId !== cropState.activePointerId) {
                return;
            }

            cropState.dragging = false;
            cropState.activePointerId = null;
            cropCanvas.classList.remove("is-dragging");
        };

        cropCanvas.addEventListener("pointerup", finishDragging);
        cropCanvas.addEventListener("pointercancel", finishDragging);
        cropCanvas.addEventListener("lostpointercapture", finishDragging);

        cropperElement.addEventListener("shown.bs.modal", () => {
            const backdrops = document.querySelectorAll(".modal-backdrop");
            backdrops[backdrops.length - 1]?.classList.add(
                "tutor-image-crop-backdrop");
            if (cropState.image) {
                cropCanvas.focus({ preventScroll: true });
            }
        });

        cropCanvas.addEventListener("keydown", (event) => {
            const movement = event.shiftKey ? 20 : 6;
            const directions = {
                ArrowLeft: [-movement, 0],
                ArrowRight: [movement, 0],
                ArrowUp: [0, -movement],
                ArrowDown: [0, movement]
            };
            const direction = directions[event.key];
            if (!direction) {
                return;
            }

            event.preventDefault();
            cropState.centerX += direction[0];
            cropState.centerY += direction[1];
            drawCrop();
        });

        cropSave.addEventListener("click", () => {
            if (!cropState.image) {
                showCropError("Choose an image before saving.");
                return;
            }

            cropSave.disabled = true;
            showCropError("");
            cropCanvas.toBlob((blob) => {
                if (!blob) {
                    cropSave.disabled = false;
                    showCropError("We couldn't prepare that image. Please try another one.");
                    return;
                }

                const croppedFile = new File(
                    [blob],
                    "profile-picture.png",
                    { type: "image/png", lastModified: Date.now() });
                try {
                    const transfer = new DataTransfer();
                    transfer.items.add(croppedFile);
                    imageInput.files = transfer.files;
                    cropState.submitting = true;
                    imageForm.requestSubmit();
                } catch {
                    cropSave.disabled = false;
                    showCropError(
                        "This browser couldn't prepare the cropped image. Please try a current browser.");
                }
            }, "image/png");
        });

        cropperElement.addEventListener("hidden.bs.modal", () => {
            if (!cropState.submitting) {
                imageInput.value = "";
            }
            document.querySelectorAll(".tutor-image-crop-backdrop").forEach(
                (backdrop) => backdrop.classList.remove(
                    "tutor-image-crop-backdrop"));
            finishDragging();
            cropState.image = null;
            cropSave.disabled = false;
            showCropError("");
        });
    }

    const phoneForm = document.querySelector("[data-phone-form]");
    const phoneInput = phoneForm?.querySelector("[data-phone-input]");

    if (phoneForm && phoneInput) {
        const initialPhoneNumber = phoneInput.value.trim();

        phoneInput.addEventListener("blur", (event) => {
            const nextFocusedElement = event.relatedTarget;
            const submitButton = phoneForm.querySelector("button[type='submit']");

            if (nextFocusedElement === submitButton ||
                phoneInput.value.trim() === initialPhoneNumber) {
                return;
            }

            if (!phoneInput.checkValidity()) {
                phoneInput.reportValidity();
                return;
            }

            phoneForm.requestSubmit();
        });
    }

    const modalElement = document.querySelector("#module-change-modal");
    if (!modalElement) {
        return;
    }

    const requestType = modalElement.querySelector("[data-module-request-type]");
    const changeTypeCombobox = modalElement.querySelector("[data-change-type-combobox]");
    const changeTypeToggle = modalElement.querySelector("[data-change-type-toggle]");
    const changeTypeMenu = modalElement.querySelector("[data-change-type-menu]");
    const changeTypeSelected = modalElement.querySelector("[data-change-type-selected]");
    const changeTypeOptions = Array.from(
        modalElement.querySelectorAll("[data-change-type-option]"));
    const moduleSelector = modalElement.querySelector("[data-module-selector]");
    const moduleSearch = modalElement.querySelector("[data-module-search]");
    const moduleCombobox = modalElement.querySelector("[data-module-combobox]");
    const moduleToggle = modalElement.querySelector("[data-module-toggle]");
    const moduleMenu = modalElement.querySelector("[data-module-menu]");
    const moduleOptions = modalElement.querySelector("[data-module-options]");
    const moduleSelected = modalElement.querySelector("[data-module-selected]");
    const selectedModuleList = modalElement.querySelector("[data-selected-module-list]");
    const selectedModuleChips = modalElement.querySelector("[data-selected-module-chips]");
    const sourceOptions = moduleSelector
        ? Array.from(moduleSelector.querySelectorAll("option[data-request-type]"))
        : [];

    const closeChangeTypeMenu = () => {
        if (!changeTypeMenu || !changeTypeToggle) {
            return;
        }

        changeTypeMenu.hidden = true;
        changeTypeToggle.setAttribute("aria-expanded", "false");
    };

    const updateSelectedChangeType = () => {
        if (!requestType || !changeTypeSelected) {
            return;
        }

        const selectedOption = requestType.selectedOptions[0];
        changeTypeSelected.textContent = selectedOption?.textContent.trim() ??
            "Select a change type";
        changeTypeOptions.forEach((option) => {
            option.setAttribute(
                "aria-selected",
                String(option.dataset.value === requestType.value));
        });
    };

    const closeModuleMenu = () => {
        if (!moduleMenu || !moduleToggle) {
            return;
        }

        moduleMenu.hidden = true;
        moduleToggle.setAttribute("aria-expanded", "false");
    };

    const openModuleMenu = () => {
        if (!moduleMenu || !moduleToggle) {
            return;
        }

        moduleMenu.hidden = false;
        moduleToggle.setAttribute("aria-expanded", "true");
        moduleSearch?.focus();
    };

    const updateSelectedModuleLabel = () => {
        if (!moduleSelector || !moduleSelected) {
            return;
        }

        const selectedOptions = Array.from(moduleSelector.selectedOptions)
            .filter((option) => option.dataset.requestType);

        if (selectedOptions.length === 0) {
            moduleSelected.textContent = "Select modules";
        } else if (selectedOptions.length === 1) {
            moduleSelected.textContent = selectedOptions[0].textContent.trim();
        } else {
            moduleSelected.textContent = `${selectedOptions.length} modules selected`;
        }

        if (!selectedModuleList || !selectedModuleChips) {
            return;
        }

        selectedModuleChips.replaceChildren();
        selectedModuleList.hidden = selectedOptions.length === 0;
        selectedOptions.forEach((option) => {
            const selectedModule = document.createElement("div");
            const copy = document.createElement("div");
            const code = document.createElement("strong");
            const name = document.createElement("span");
            const removeButton = document.createElement("button");

            selectedModule.className = "tutor-selected-module";
            copy.className = "tutor-selected-module-copy";
            code.textContent = option.dataset.moduleCode ?? "Module";
            name.textContent = option.dataset.moduleName ?? option.textContent.trim();
            copy.append(code, name);

            removeButton.type = "button";
            removeButton.className = "tutor-selected-module-remove";
            removeButton.setAttribute(
                "aria-label",
                `Remove ${option.textContent.trim()} from selected modules`);

            const removeIcon = document.createElementNS(
                "http://www.w3.org/2000/svg",
                "svg");
            removeIcon.setAttribute("viewBox", "0 0 24 24");
            removeIcon.setAttribute("aria-hidden", "true");
            const removePath = document.createElementNS(
                "http://www.w3.org/2000/svg",
                "path");
            removePath.setAttribute("d", "M6 6l12 12M18 6L6 18");
            removePath.setAttribute("fill", "none");
            removePath.setAttribute("stroke", "currentColor");
            removePath.setAttribute("stroke-linecap", "round");
            removePath.setAttribute("stroke-width", "2");
            removeIcon.append(removePath);
            removeButton.append(removeIcon);
            removeButton.addEventListener("click", () => {
                option.selected = false;
                moduleSelector.dispatchEvent(new Event("change", { bubbles: true }));
                refreshModuleOptions();
            });

            selectedModule.append(copy, removeButton);
            selectedModuleChips.append(selectedModule);
        });
    };

    const refreshModuleOptions = () => {
        if (!requestType || !moduleSelector || !moduleOptions) {
            return;
        }

        const selectedType = requestType.value;
        const searchTerm = moduleSearch?.value.trim().toLocaleLowerCase() ?? "";
        sourceOptions.forEach((option) => {
            if (option.dataset.requestType !== selectedType) {
                option.selected = false;
            }
        });

        const matches = sourceOptions.filter((option) =>
            option.dataset.requestType === selectedType &&
            option.textContent.toLocaleLowerCase().includes(searchTerm));

        moduleOptions.replaceChildren();

        if (matches.length === 0) {
            const emptyMessage = document.createElement("p");
            emptyMessage.className = "tutor-module-combobox-empty";
            emptyMessage.textContent = "No matching modules found.";
            moduleOptions.append(emptyMessage);
        } else {
            matches.forEach((option) => {
                const optionButton = document.createElement("button");
                optionButton.type = "button";
                optionButton.className =
                    "tutor-module-combobox-option tutor-module-combobox-option--multi";
                optionButton.textContent = option.textContent.trim();
                optionButton.setAttribute("role", "option");
                optionButton.setAttribute(
                    "aria-selected",
                    String(option.selected));
                optionButton.addEventListener("click", () => {
                    option.selected = !option.selected;
                    moduleSelector.dispatchEvent(new Event("change", { bubbles: true }));
                    optionButton.setAttribute("aria-selected", String(option.selected));
                    updateSelectedModuleLabel();
                });
                moduleOptions.append(optionButton);
            });
        }

        updateSelectedModuleLabel();
    };

    changeTypeToggle?.addEventListener("click", () => {
        if (changeTypeMenu?.hidden) {
            closeModuleMenu();
            changeTypeMenu.hidden = false;
            changeTypeToggle.setAttribute("aria-expanded", "true");
        } else {
            closeChangeTypeMenu();
        }
    });
    changeTypeOptions.forEach((option) => {
        option.addEventListener("click", () => {
            if (!requestType) {
                return;
            }

            requestType.value = option.dataset.value ?? "";
            requestType.dispatchEvent(new Event("change", { bubbles: true }));
            updateSelectedChangeType();
            closeChangeTypeMenu();
            changeTypeToggle?.focus();
        });
    });
    requestType?.addEventListener("change", () => {
        if (moduleSearch) {
            moduleSearch.value = "";
        }
        updateSelectedChangeType();
        refreshModuleOptions();
    });
    moduleSearch?.addEventListener("input", refreshModuleOptions);
    moduleToggle?.addEventListener("click", () => {
        if (moduleMenu?.hidden) {
            closeChangeTypeMenu();
            openModuleMenu();
        } else {
            closeModuleMenu();
        }
    });
    moduleSearch?.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeModuleMenu();
            moduleToggle?.focus();
        }
    });
    document.addEventListener("click", (event) => {
        if (moduleCombobox && !moduleCombobox.contains(event.target)) {
            closeModuleMenu();
        }
        if (changeTypeCombobox && !changeTypeCombobox.contains(event.target)) {
            closeChangeTypeMenu();
        }
    });
    modalElement.addEventListener("hidden.bs.modal", () => {
        closeModuleMenu();
        closeChangeTypeMenu();
    });
    updateSelectedChangeType();
    refreshModuleOptions();

});
