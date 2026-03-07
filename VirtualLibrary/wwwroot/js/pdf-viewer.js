document.addEventListener('DOMContentLoaded', function () {
    const generateAudioBtn = document.getElementById('generateAudioBtn');
    const audioPlayerContainer = document.getElementById('audioPlayerContainer');
    const pdfAudioPlayer = document.getElementById('pdfAudioPlayer');
    const processingIndicator = document.getElementById('processingIndicator');
    const progressBar = document.getElementById('progressBar');
    const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    let pollInterval = null;
    let fakeProgressInterval = null;
    let progressPercent = 0;

    if (generateAudioBtn) {
        generateAudioBtn.addEventListener('click', async function () {
            const generateUrl = generateAudioBtn.dataset.generateUrl;
            const statusUrl = generateAudioBtn.dataset.statusUrl;

            if (pdfAudioPlayer && pdfAudioPlayer.getAttribute('src')) {
                audioPlayerContainer.style.display = 'block';
                audioPlayerContainer.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                pdfAudioPlayer.play().catch(() => { });
                return;
            }

            setButtonStarting();
            showProcessing();
            startFakeProgress();

            try {
                const response = await fetch(generateUrl, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': antiForgeryToken || ''
                    }
                });

                const data = await parseResponse(response);

                if (!response.ok) {
                    showError(data.error || 'Could not start audiobook generation.');
                    return;
                }

                if (data.status === 'Completed' && data.audioUrl) {
                    completeProgress();
                    showAudioReady(data.audioUrl);
                    return;
                }

                if (data.status === 'Processing') {
                    setButtonProcessing();
                    startPolling(statusUrl);
                    return;
                }

                showError(data.error || 'Unexpected server response.');
            } catch {
                showError('Could not connect to the server.');
            }
        });
    }

    async function parseResponse(response) {
        const text = await response.text();

        if (!text)
            return {};

        try {
            return JSON.parse(text);
        } catch {
            return { error: text };
        }
    }

    function showProcessing() {
        if (processingIndicator)
            processingIndicator.style.display = 'block';
    }

    function hideProcessing() {
        if (processingIndicator)
            processingIndicator.style.display = 'none';
    }

    function startFakeProgress() {
        if (!progressBar)
            return;

        resetProgress();
        fakeProgressInterval = window.setInterval(function () {
            if (progressPercent >= 90) {
                clearInterval(fakeProgressInterval);
                fakeProgressInterval = null;
                return;
            }

            progressPercent += Math.max(1, (90 - progressPercent) * 0.08);
            progressBar.style.width = progressPercent + '%';
        }, 400);
    }

    function completeProgress() {
        if (fakeProgressInterval) {
            clearInterval(fakeProgressInterval);
            fakeProgressInterval = null;
        }

        if (progressBar)
            progressBar.style.width = '100%';
    }

    function resetProgress() {
        if (fakeProgressInterval) {
            clearInterval(fakeProgressInterval);
            fakeProgressInterval = null;
        }

        progressPercent = 0;

        if (progressBar)
            progressBar.style.width = '0%';
    }

    function startPolling(statusUrl) {
        clearPolling();

        let attempts = 0;
        const maxAttempts = 90;

        pollInterval = window.setInterval(async function () {
            attempts++;

            if (attempts > maxAttempts) {
                clearPolling();
                showError('Generation is taking longer than expected. Please try again later.');
                return;
            }

            try {
                const response = await fetch(statusUrl, { cache: 'no-store' });
                const data = await parseResponse(response);

                if (!response.ok) {
                    clearPolling();
                    showError(data.error || 'Could not read audiobook status.');
                    return;
                }

                if (data.status === 'Completed' && data.audioUrl) {
                    clearPolling();
                    completeProgress();
                    showAudioReady(data.audioUrl);
                    return;
                }

                if (data.status === 'Failed') {
                    clearPolling();
                    showError(data.error || 'Audiobook generation failed.');
                }
            } catch {
            }
        }, 2000);
    }

    function clearPolling() {
        if (pollInterval) {
            clearInterval(pollInterval);
            pollInterval = null;
        }
    }

    function showAudioReady(audioUrl) {
        hideProcessing();

        if (pdfAudioPlayer) {
            pdfAudioPlayer.src = audioUrl;
            pdfAudioPlayer.setAttribute('src', audioUrl);
            pdfAudioPlayer.load();
        }

        if (audioPlayerContainer) {
            audioPlayerContainer.style.display = 'block';
            audioPlayerContainer.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }

        if (generateAudioBtn) {
            generateAudioBtn.innerHTML = '<i class="bx bx-check-circle"></i> ' + (generateAudioBtn.dataset.readyLabel || 'Audio Ready - Click to Play');
            generateAudioBtn.style.background = '#2BD47D';
            generateAudioBtn.style.color = '#0A2558';
            generateAudioBtn.disabled = false;
        }

        if (pdfAudioPlayer) {
            pdfAudioPlayer.play().catch(() => { });
        }
    }

    function showError(message) {
        clearPolling();
        hideProcessing();
        resetProgress();
        alert(message);

        if (generateAudioBtn) {
            generateAudioBtn.innerHTML = '<i class="bx bx-headphone"></i> ' + (generateAudioBtn.dataset.defaultLabel || 'Generate Audiobook');
            generateAudioBtn.style.background = '#6f42c1';
            generateAudioBtn.style.color = '#fff';
            generateAudioBtn.disabled = false;
        }
    }

    function setButtonStarting() {
        if (!generateAudioBtn)
            return;

        generateAudioBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Starting...';
        generateAudioBtn.disabled = true;
    }

    function setButtonProcessing() {
        if (!generateAudioBtn)
            return;

        generateAudioBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Generating...';
        generateAudioBtn.disabled = true;
    }

    const uploadArea = document.getElementById('uploadArea');
    const fileInput = document.getElementById('fileInput');

    if (uploadArea && fileInput) {
        uploadArea.addEventListener('click', function (e) {
            if (e.target === fileInput)
                return;

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

                uploadArea.innerHTML =
                    '<i class="bx bxs-check-circle upload-success-icon"></i>' +
                    '<strong class="upload-file-name">' + file.name + '</strong>' +
                    '<span class="upload-file-size">' + sizeMB + ' MB - Ready to upload</span>';
            }
        }
    }
});