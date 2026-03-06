document.addEventListener('DOMContentLoaded', function () {
    const audio = document.getElementById('audioPlayer');
    const playPauseBtn = document.getElementById('playPauseBtn');
    const muteBtn = document.getElementById('muteBtn');
    const currentTimeEl = document.getElementById('currentTime');
    const durationEl = document.getElementById('duration');
    const progressBar = document.getElementById('progressBar');
    const progressFill = document.getElementById('progressFill');

    if (audio) {
        if (playPauseBtn) {
            playPauseBtn.addEventListener('click', function () {
                if (audio.paused) {
                    audio.play();
                    playPauseBtn.innerHTML = '<i class="bi bi-pause-fill"></i> Pause';
                } else {
                    audio.pause();
                    playPauseBtn.innerHTML = '<i class="bi bi-play-fill"></i> Play';
                }
            });
        }

        audio.addEventListener('loadedmetadata', function () {
            durationEl.textContent = formatTime(audio.duration);
        });

        audio.addEventListener('timeupdate', function () {
            currentTimeEl.textContent = formatTime(audio.currentTime);
            const percent = (audio.currentTime / audio.duration) * 100;
            progressFill.style.width = percent + '%';
        });

        if (progressBar) {
            progressBar.addEventListener('click', function (e) {
                const rect = progressBar.getBoundingClientRect();
                const percent = (e.clientX - rect.left) / rect.width;
                audio.currentTime = percent * audio.duration;
            });
        }

        if (muteBtn) {
            muteBtn.addEventListener('click', function () {
                audio.muted = !audio.muted;
                if (audio.muted) {
                    muteBtn.innerHTML = '<i class="bi bi-volume-mute"></i>';
                } else {
                    muteBtn.innerHTML = '<i class="bi bi-volume-up"></i>';
                }
            });
        }

        audio.addEventListener('play', function () {
            if (playPauseBtn) {
                playPauseBtn.innerHTML = '<i class="bi bi-pause-fill"></i> Pause';
            }
        });

        audio.addEventListener('pause', function () {
            if (playPauseBtn) {
                playPauseBtn.innerHTML = '<i class="bi bi-play-fill"></i> Play';
            }
        });
    }

    function formatTime(seconds) {
        if (!seconds || isNaN(seconds)) return '0:00';
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }

    const generateAudioBtn = document.getElementById('generateAudioBtn');
    const audioPlayerContainer = document.getElementById('audioPlayerContainer');
    const pdfAudioPlayer = document.getElementById('pdfAudioPlayer');
    const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    if (generateAudioBtn) {
        generateAudioBtn.addEventListener('click', async function () {
            const productId = this.getAttribute('data-product-id');
            const originalText = this.innerHTML;

            this.innerHTML = '<i class="bx bx-loader bx-spin"></i> Generating Audio...';
            this.disabled = true;

            try {
                const url = window.location.pathname + '?handler=GenerateAudio';
                const response = await fetch(url, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': antiForgeryToken
                    },
                    body: JSON.stringify({ productId: parseInt(productId) })
                });

                const data = await response.json();

                if (response.ok && data.audioUrl) {
                    pdfAudioPlayer.src = data.audioUrl;
                    audioPlayerContainer.style.display = 'block';
                    pdfAudioPlayer.play();
                    this.innerHTML = '<i class="bx bx-check"></i> Audio Ready';
                } else {
                    alert('Error generating audio: ' + (data.error || 'Unknown error'));
                    this.innerHTML = originalText;
                    this.disabled = false;
                }
            } catch (error) {
                alert('Failed to connect to the server.');
                this.innerHTML = originalText;
                this.disabled = false;
            }
        });
    }
});