document.addEventListener('DOMContentLoaded', function () {
    const audio = document.getElementById('audioPlayer');
    const playPauseBtn = document.getElementById('playPauseBtn');
    const muteBtn = document.getElementById('muteBtn');
    const currentTimeEl = document.getElementById('currentTime');
    const durationEl = document.getElementById('duration');
    const progressBar = document.getElementById('progressBar');
    const progressFill = document.getElementById('progressFill');

    if (!audio) return;

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

    function formatTime(seconds) {
        if (!seconds || isNaN(seconds)) return '0:00';
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }
});