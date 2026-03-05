document.addEventListener('DOMContentLoaded', function () {
    const uploadArea = document.getElementById('uploadArea');
    const fileInput = document.getElementById('fileInput');

    if (!uploadArea || !fileInput) return;

    // Click pe zona de upload (dar NU pe butonul de submit)
    uploadArea.addEventListener('click', function (e) {
        // Previne propagarea daca se da click pe input-ul de file direct
        if (e.target === fileInput) return;
        fileInput.click();
    });

    uploadArea.addEventListener('dragover', function (e) {
        e.preventDefault();
        e.stopPropagation();
        uploadArea.classList.add('drag-over');
    });

    uploadArea.addEventListener('dragleave', function (e) {
        e.preventDefault();
        e.stopPropagation();
        uploadArea.classList.remove('drag-over');
    });

    uploadArea.addEventListener('drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
        uploadArea.classList.remove('drag-over');

        const files = e.dataTransfer.files;
        if (files.length > 0) {
            fileInput.files = files;
            updateUploadAreaDisplay();
        }
    });

    fileInput.addEventListener('change', function () {
        updateUploadAreaDisplay();
    });

    function updateUploadAreaDisplay() {
        if (fileInput.files && fileInput.files.length > 0) {
            const file = fileInput.files[0];
            const sizeMB = (file.size / 1024 / 1024).toFixed(2);
            uploadArea.innerHTML = `
                <i class='bx bxs-check-circle' style="font-size: 2em; color: #28a745;"></i>
                <strong style="display:block; margin-top: 10px;">${file.name}</strong>
                <span style="color: #6c757d; font-size: 0.85em;">${sizeMB} MB — Ready to upload</span>
            `;
        }
    }
});