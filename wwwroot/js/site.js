document.addEventListener('DOMContentLoaded', function () {
    initToast();
    initImagePickers();
    initImageLightbox();
    initThemeToggle();
});

function initToast() {
    const toastEl = document.querySelector('.toast');
    if (toastEl && window.bootstrap) {
        const toast = new bootstrap.Toast(toastEl);
        toast.show();
    }
}

function initImagePickers() {
    const pickers = document.querySelectorAll('[data-image-picker]');
    pickers.forEach(function (picker) {
        const controls = picker.querySelector('.d-flex');
        const initialInput = picker.querySelector('[data-image-input]');
        const chooseButton = picker.querySelector('[data-image-pick]');
        const clearButton = picker.querySelector('[data-image-clear]');
        const list = picker.querySelector('[data-image-list]');

        if (!controls || !initialInput || !list) {
            return;
        }

        let activeInput = initialInput;
        let groupCounter = 0;
        let groups = [];
        let previewUrls = [];

        function revokePreviewUrls() {
            previewUrls.forEach(function (url) {
                URL.revokeObjectURL(url);
            });
            previewUrls = [];
        }

        function formatSize(bytes) {
            if (bytes < 1024) {
                return bytes + ' B';
            }

            const kb = bytes / 1024;
            if (kb < 1024) {
                return kb.toFixed(1) + ' KB';
            }

            return (kb / 1024).toFixed(2) + ' MB';
        }

        function createNextInput() {
            const next = activeInput.cloneNode();
            next.value = '';
            next.classList.remove('visually-hidden');
            next.setAttribute('data-image-input', 'true');

            next.addEventListener('change', onActiveInputChange);
            controls.insertBefore(next, chooseButton || clearButton || null);
            activeInput = next;
        }

        function addSelectionAsGroup(fileList) {
            const files = Array.from(fileList || []);
            if (files.length === 0) {
                return;
            }

            const id = 'group-' + (++groupCounter);

            activeInput.classList.add('visually-hidden');
            activeInput.removeAttribute('data-image-input');
            activeInput.setAttribute('data-image-group-id', id);

            groups.push({
                id: id,
                input: activeInput,
                files: files
            });

            createNextInput();
            renderList();
        }

        function removeGroup(groupId) {
            const group = groups.find(function (g) { return g.id === groupId; });
            if (!group) {
                return;
            }

            if (group.input && group.input.parentElement) {
                group.input.parentElement.removeChild(group.input);
            }

            groups = groups.filter(function (g) { return g.id !== groupId; });
            renderList();
        }

        function clearAllSelections() {
            groups.forEach(function (group) {
                if (group.input && group.input.parentElement) {
                    group.input.parentElement.removeChild(group.input);
                }
            });

            groups = [];
            activeInput.value = '';
            renderList();
        }

        function renderList() {
            revokePreviewUrls();
            list.innerHTML = '';

            if (groups.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'image-picker-empty';
                empty.textContent = 'Noch keine neuen Bilder ausgewaehlt.';
                list.appendChild(empty);
                return;
            }

            groups.forEach(function (group) {
                const totalBytes = group.files.reduce(function (sum, file) {
                    return sum + file.size;
                }, 0);

                const title = group.files.length === 1
                    ? group.files[0].name
                    : group.files.length + ' Dateien ausgewaehlt';

                const row = document.createElement('div');
                row.className = 'image-chip';

                const previewUrl = URL.createObjectURL(group.files[0]);
                previewUrls.push(previewUrl);

                const preview = document.createElement('a');
                preview.href = previewUrl;
                preview.className = 'gallery-image-link image-chip-thumb';
                preview.setAttribute('data-full-src', previewUrl);
                preview.setAttribute('data-caption', title);

                const img = document.createElement('img');
                img.src = previewUrl;
                img.alt = title;
                preview.appendChild(img);

                const meta = document.createElement('div');
                meta.className = 'image-chip-meta';

                const nameEl = document.createElement('div');
                nameEl.className = 'image-chip-name';
                nameEl.textContent = title;

                const sizeEl = document.createElement('div');
                sizeEl.className = 'image-chip-size';
                sizeEl.textContent = formatSize(totalBytes);

                meta.appendChild(nameEl);
                meta.appendChild(sizeEl);

                const removeButton = document.createElement('button');
                removeButton.type = 'button';
                removeButton.className = 'btn btn-outline-danger btn-sm';
                removeButton.setAttribute('data-image-remove-group', group.id);
                removeButton.textContent = 'Auswahl entfernen';

                row.appendChild(preview);
                row.appendChild(meta);
                row.appendChild(removeButton);
                list.appendChild(row);
            });
        }

        function onActiveInputChange(event) {
            if (event.target.files && event.target.files.length > 0) {
                addSelectionAsGroup(event.target.files);
            }
        }

        activeInput.addEventListener('change', onActiveInputChange);

        if (chooseButton) {
            chooseButton.addEventListener('click', function () {
                activeInput.click();
            });
        }

        if (clearButton) {
            clearButton.addEventListener('click', clearAllSelections);
        }

        list.addEventListener('click', function (event) {
            const removeButton = event.target.closest('[data-image-remove-group]');
            if (!removeButton) {
                return;
            }

            const groupId = removeButton.getAttribute('data-image-remove-group');
            if (groupId) {
                removeGroup(groupId);
            }
        });

        renderList();
    });
}

function initThemeToggle() {
    var btn = document.getElementById('theme-toggle');
    var icon = document.getElementById('theme-icon');
    if (!btn || !icon) return;

    function update() {
        var theme = document.documentElement.getAttribute('data-theme') || 'light';
        icon.innerHTML = theme === 'dark' ? '&#9728;' : '&#9790;';
    }

    update();
    btn.addEventListener('click', function () {
        var next = (document.documentElement.getAttribute('data-theme') || 'light') === 'dark'
            ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', next);
        localStorage.setItem('theme', next);
        update();
    });
}

function initImageLightbox() {
    const modalEl = document.getElementById('imageLightboxModal');
    const imgEl = document.getElementById('imageLightboxImage');
    const captionEl = document.getElementById('imageLightboxCaption');

    if (!modalEl || !imgEl || !captionEl || !window.bootstrap) {
        return;
    }

    const modal = new bootstrap.Modal(modalEl);

    document.addEventListener('click', function (event) {
        const link = event.target.closest('.gallery-image-link');
        if (!link) {
            return;
        }

        event.preventDefault();

        const src =
            link.getAttribute('data-full-src') ||
            link.getAttribute('href') ||
            (link.querySelector('img') ? link.querySelector('img').src : '');

        if (!src) {
            return;
        }

        const caption =
            link.getAttribute('data-caption') ||
            (link.querySelector('img') ? link.querySelector('img').alt : '') ||
            '';

        imgEl.src = src;
        imgEl.alt = caption || 'Bild';
        captionEl.textContent = caption;

        modal.show();
    });

    modalEl.addEventListener('hidden.bs.modal', function () {
        imgEl.src = '';
        imgEl.alt = 'Bild';
        captionEl.textContent = '';
    });
}
