
document.addEventListener('DOMContentLoaded', function () {
    const processingElements = document.querySelectorAll('[class*="bg-info"]');

    let hasProcessing = false;
    processingElements.forEach(el => {
        if (el.textContent.includes('Processing')) {
            hasProcessing = true;
        }
    });

    if (hasProcessing) {
        setInterval(() => {
            location.reload();
        }, 5000);
    }
});