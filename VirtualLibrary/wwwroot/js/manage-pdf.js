document.addEventListener('DOMContentLoaded', function () {
    const uploadArea = document.getElementById('uploadArea');
    const fileInput = document.getElementById('fileInput');

    if (!uploadArea || !fileInput) return;

    uploadArea.addEventListener('click', function () {
        fileInput.click();
    });

    uploadArea.addEventListener('dragover', function (e) {
        e.preventDefault();
        uploadArea.style.backgroundColor = '#e7f1ff';
        uploadArea.style.borderColor = '#764ba2';
    });

    uploadArea.addEventListener('dragleave', function () {
        uploadArea.style.backgroundColor = '#fff';
        uploadArea.style.borderColor = '#0d6efd';
    });

    uploadArea.addEventListener('drop', function (e) {
        e.preventDefault();
        uploadArea.style.backgroundColor = '#fff';
        uploadArea.style.borderColor = '#0d6efd';

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
        if (fileInput.files.length > 0) {
            const fileName = fileInput.files[0].name;
            uploadArea.innerHTML = `
                <i class="bi bi-check-circle text-success" style="font-size: 2em;"></i>
                <p class="mt-3 mb-0">
                    <strong>${fileName}</strong><br>
                    <small class="text-muted">Ready to upload</small>
                </p>
            `;
        }
    }
});