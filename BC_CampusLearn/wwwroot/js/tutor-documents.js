(() => {
    const form = document.querySelector('[data-documents-form]');
    if (!form) return;

    const signaturePad = form.querySelector('[data-signature-pad]');
    const signatureShell = form.querySelector('[data-signature-shell]');
    const signatureData = form.querySelector('[data-signature-data]');
    const signatureCleared = form.querySelector('[data-signature-cleared]');
    const signatureClear = form.querySelector('[data-signature-clear]');
    const signatureError = form.querySelector('[data-signature-error]');
    let isDrawing = false;
    let signatureChanged = false;

    const setSignatureError = (message) => {
        signatureShell?.classList.toggle('is-invalid', Boolean(message));
        signaturePad?.setAttribute('aria-invalid', message ? 'true' : 'false');
        if (signatureError) {
            signatureError.textContent = message;
            signatureError.classList.toggle('field-validation-error', Boolean(message));
            signatureError.classList.toggle('field-validation-valid', !message);
        }
        return !message;
    };

    const signatureContext = signaturePad?.getContext('2d');
    const pointFor = (event) => {
        const bounds = signaturePad.getBoundingClientRect();
        return {
            x: (event.clientX - bounds.left) * (signaturePad.width / bounds.width),
            y: (event.clientY - bounds.top) * (signaturePad.height / bounds.height)
        };
    };

    const commitSignature = () => {
        if (!signaturePad || !signatureData || !signatureChanged) return;
        signatureData.value = signaturePad.toDataURL('image/png');
        signatureCleared.value = 'false';
        signatureChanged = false;
    };

    if (signaturePad && signatureContext && signatureData) {
        signatureContext.strokeStyle = '#111';
        signatureContext.lineWidth = 3.5;
        signatureContext.lineCap = 'round';
        signatureContext.lineJoin = 'round';

        if (signatureData.value) {
            const storedSignature = new Image();
            storedSignature.addEventListener('load', () => {
                signatureContext.drawImage(
                    storedSignature,
                    0,
                    0,
                    signaturePad.width,
                    signaturePad.height);
            });
            storedSignature.src = signatureData.value;
        }

        signaturePad.addEventListener('pointerdown', (event) => {
            event.preventDefault();
            isDrawing = true;
            signatureChanged = true;
            signaturePad.setPointerCapture(event.pointerId);
            const point = pointFor(event);
            signatureContext.beginPath();
            signatureContext.moveTo(point.x, point.y);
            setSignatureError('');
        });
        signaturePad.addEventListener('pointermove', (event) => {
            if (!isDrawing) return;
            event.preventDefault();
            const point = pointFor(event);
            signatureContext.lineTo(point.x, point.y);
            signatureContext.stroke();
        });
        const finishDrawing = (event) => {
            if (!isDrawing) return;
            isDrawing = false;
            signatureContext.closePath();
            if (signaturePad.hasPointerCapture(event.pointerId)) {
                signaturePad.releasePointerCapture(event.pointerId);
            }
            commitSignature();
        };
        signaturePad.addEventListener('pointerup', finishDrawing);
        signaturePad.addEventListener('pointercancel', finishDrawing);

        signatureClear?.addEventListener('click', () => {
            signatureContext.clearRect(0, 0, signaturePad.width, signaturePad.height);
            signatureData.value = '';
            signatureCleared.value = 'true';
            signatureChanged = false;
            setSignatureError('');
            signaturePad.focus();
        });
    }

    const messageFor = (field) => form.querySelector(
        `[data-valmsg-for="${CSS.escape(field.name)}"]`);

    const setError = (field, message) => {
        const target = messageFor(field);
        field.classList.toggle('is-invalid', Boolean(message));
        field.setAttribute('aria-invalid', message ? 'true' : 'false');
        if (target) {
            target.textContent = message;
            target.classList.toggle('field-validation-error', Boolean(message));
            target.classList.toggle('field-validation-valid', !message);
        }
        return !message;
    };

    const labelText = (field) => {
        const label = form.querySelector(`label[for="${CSS.escape(field.id)}"]`);
        return label?.textContent.replace('*', '').trim() || 'This field';
    };

    const validate = (field, completing) => {
        const value = field.type === 'checkbox' ? field.checked : field.value.trim();
        const isRequired = field.hasAttribute('data-required') ||
            (completing && field.hasAttribute('data-completion-required'));

        if (isRequired && (field.type === 'checkbox' ? !value : value.length === 0)) {
            const message = field.type === 'checkbox'
                ? 'Confirm that the typed name is your electronic signature.'
                : `${labelText(field)} is required.`;
            return setError(field, message);
        }
        if (value && field.hasAttribute('data-email') &&
            !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) {
            return setError(field, 'Enter a valid email address.');
        }
        if (value && field.hasAttribute('data-phone') &&
            !/^[+\d][\d\s()-]{6,31}$/.test(value)) {
            return setError(field, 'Enter a valid phone number.');
        }
        if (value && field.hasAttribute('data-date')) {
            const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
            if (!match) return setError(field, 'Enter the date as dd/mm/yyyy.');
            const day = Number(match[1]);
            const month = Number(match[2]);
            const year = Number(match[3]);
            const date = new Date(Date.UTC(year, month - 1, day));
            if (date.getUTCFullYear() !== year ||
                date.getUTCMonth() !== month - 1 ||
                date.getUTCDate() !== day) {
                return setError(field, 'Enter a valid date as dd/mm/yyyy.');
            }
        }
        return setError(field, '');
    };

    const fields = [...form.querySelectorAll(
        '[data-required], [data-completion-required], [data-email], [data-phone], [data-date]')];

    fields.forEach((field) => {
        const eventName = field.type === 'checkbox' ? 'change' : 'blur';
        field.addEventListener(eventName, () => validate(field, false));
        field.addEventListener('input', () => {
            if (field.hasAttribute('data-date')) {
                const digits = field.value.replace(/\D/g, '').slice(0, 8);
                field.value = [
                    digits.slice(0, 2),
                    digits.slice(2, 4),
                    digits.slice(4, 8)
                ].filter(Boolean).join('/');
            }
            if (field.classList.contains('is-invalid')) validate(field, false);
        });
    });

    form.addEventListener('submit', (event) => {
        const completing = event.submitter?.hasAttribute('data-complete') === true;
        commitSignature();
        let firstInvalid = null;
        fields.forEach((field) => {
            if (!validate(field, completing) && !firstInvalid) firstInvalid = field;
        });
        if (completing && !signatureData?.value) {
            setSignatureError('Draw your electronic signature before previewing the PDF.');
            firstInvalid ??= signaturePad;
        } else {
            setSignatureError('');
        }
        if (!firstInvalid) return;

        event.preventDefault();
        firstInvalid.focus();
        firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
})();
