// camera.js – Módulo JS para acceso a la cámara nativa del dispositivo
// Usado via JSInterop desde Blazor

/**
 * Abre la cámara del dispositivo, captura una foto y devuelve:
 * { dataUrl: "data:image/jpeg;base64,...", mimeType: "image/jpeg", bytes: Uint8Array }
 * Si el usuario cancela o hay error devuelve null.
 */
window.cameraModule = {

    /**
     * Captura foto usando el input[type=file] con capture="environment"
     * (activa directamente la cámara trasera en móvil).
     * Devuelve { base64: string, mimeType: string } o null si cancela.
     */
    capturePhoto: function () {
        return new Promise((resolve) => {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = 'image/*';
            input.capture = 'environment'; // cámara trasera
            input.style.display = 'none';
            document.body.appendChild(input);

            input.onchange = () => {
                const file = input.files && input.files[0];
                document.body.removeChild(input);
                if (!file) { resolve(null); return; }

                const reader = new FileReader();
                reader.onload = (e) => {
                    const dataUrl = e.target.result;
                    // Extraer solo el base64 (sin el prefijo data:...)
                    const base64 = dataUrl.split(',')[1];
                    resolve({ base64: base64, mimeType: file.type || 'image/jpeg', dataUrl: dataUrl });
                };
                reader.onerror = () => { resolve(null); };
                reader.readAsDataURL(file);
            };

            // Si el usuario cancela sin seleccionar (focus de vuelta a la ventana)
            input.oncancel = () => {
                document.body.removeChild(input);
                resolve(null);
            };

            // Fallback: si el foco vuelve sin cambio (escritorio)
            const focusHandler = () => {
                setTimeout(() => {
                    if (!input.files || input.files.length === 0) {
                        if (document.body.contains(input)) document.body.removeChild(input);
                        resolve(null);
                    }
                    window.removeEventListener('focus', focusHandler);
                }, 500);
            };
            window.addEventListener('focus', focusHandler);

            input.click();
        });
    },

    /**
     * Selecciona foto de la galería (sin capture)
     */
    selectFromGallery: function () {
        return new Promise((resolve) => {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = 'image/*';
            input.style.display = 'none';
            document.body.appendChild(input);

            input.onchange = () => {
                const file = input.files && input.files[0];
                document.body.removeChild(input);
                if (!file) { resolve(null); return; }

                const reader = new FileReader();
                reader.onload = (e) => {
                    const dataUrl = e.target.result;
                    const base64 = dataUrl.split(',')[1];
                    resolve({ base64: base64, mimeType: file.type || 'image/jpeg', dataUrl: dataUrl });
                };
                reader.onerror = () => { resolve(null); };
                reader.readAsDataURL(file);
            };

            const focusHandler = () => {
                setTimeout(() => {
                    if (!input.files || input.files.length === 0) {
                        if (document.body.contains(input)) document.body.removeChild(input);
                        resolve(null);
                    }
                    window.removeEventListener('focus', focusHandler);
                }, 500);
            };
            window.addEventListener('focus', focusHandler);

            input.click();
        });
    }
};

