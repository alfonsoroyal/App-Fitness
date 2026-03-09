// camera.js – Módulo JS para acceso a la cámara nativa del dispositivo

/**
 * Redimensiona y comprime una imagen a máximo maxSize px en el lado más largo,
 * devuelve { base64, mimeType, dataUrl } con la imagen comprimida a JPEG 0.82.
 */
function compressImage(file, maxSize = 1024) {
    return new Promise((resolve) => {
        const img = new Image();
        const url = URL.createObjectURL(file);
        img.onload = () => {
            URL.revokeObjectURL(url);
            let { width, height } = img;
            if (width > maxSize || height > maxSize) {
                if (width > height) { height = Math.round(height * maxSize / width); width = maxSize; }
                else                { width = Math.round(width * maxSize / height); height = maxSize; }
            }
            const canvas = document.createElement('canvas');
            canvas.width  = width;
            canvas.height = height;
            canvas.getContext('2d').drawImage(img, 0, 0, width, height);
            const dataUrl = canvas.toDataURL('image/jpeg', 0.82);
            const base64  = dataUrl.split(',')[1];
            resolve({ base64, mimeType: 'image/jpeg', dataUrl });
        };
        img.onerror = () => { URL.revokeObjectURL(url); resolve(null); };
        img.src = url;
    });
}

window.cameraModule = {
    capturePhoto: function () {
        return new Promise((resolve) => {
            const input = document.createElement('input');
            input.type = 'file'; input.accept = 'image/*'; input.capture = 'environment';
            input.style.display = 'none';
            document.body.appendChild(input);

            input.onchange = async () => {
                const file = input.files && input.files[0];
                document.body.removeChild(input);
                if (!file) { resolve(null); return; }
                resolve(await compressImage(file));
            };
            input.oncancel = () => { document.body.removeChild(input); resolve(null); };

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

    selectFromGallery: function () {
        return new Promise((resolve) => {
            const input = document.createElement('input');
            input.type = 'file'; input.accept = 'image/*';
            input.style.display = 'none';
            document.body.appendChild(input);

            input.onchange = async () => {
                const file = input.files && input.files[0];
                document.body.removeChild(input);
                if (!file) { resolve(null); return; }
                resolve(await compressImage(file));
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

