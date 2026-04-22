document.addEventListener("DOMContentLoaded", () => {
    const input = document.getElementById("librarySearchInput");
    const box = document.getElementById("libraryAutocompleteBox");

    if (!input || !box || !window.libraryAutocompleteConfig || !window.libraryAutocompleteConfig.endpoint) {
        return;
    }

    const endpoint = window.libraryAutocompleteConfig.endpoint;
    let currentIndex = -1;
    let abortController = null;

    function hideBox() {
        box.innerHTML = "";
        box.style.display = "none";
        currentIndex = -1;
    }

    function escapeHtml(value) {
        return String(value)
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function renderItems(items) {
        if (!items || items.length === 0) {
            hideBox();
            return;
        }

        const validItems = items.filter(item => item && item.url && item.title);

        if (!validItems.length) {
            hideBox();
            return;
        }

        box.innerHTML = validItems.map((item, index) => `
            <a href="${item.url}" class="autocomplete-item" data-index="${index}">
                <span class="autocomplete-title">${escapeHtml(item.title)}</span>
            </a>
        `).join("");

        box.style.display = "block";
        currentIndex = -1;
    }

    function getItems() {
        return [...box.querySelectorAll(".autocomplete-item")];
    }

    function setActive(items) {
        items.forEach(item => item.classList.remove("active"));

        if (currentIndex >= 0 && currentIndex < items.length) {
            items[currentIndex].classList.add("active");
        }
    }

    async function fetchSuggestions(term) {
        if (abortController) {
            abortController.abort();
        }

        abortController = new AbortController();

        try {
            const separator = endpoint.includes("?") ? "&" : "?";
            const response = await fetch(`${endpoint}${separator}term=${encodeURIComponent(term)}`, {
                signal: abortController.signal
            });

            if (!response.ok) {
                hideBox();
                return;
            }

            const data = await response.json();
            renderItems(data);
        } catch (error) {
            if (error.name !== "AbortError") {
                hideBox();
            }
        }
    }

    input.addEventListener("input", async () => {
        const term = input.value.trim();

        if (term.length < 1) {
            hideBox();
            return;
        }

        await fetchSuggestions(term);
    });

    input.addEventListener("keydown", e => {
        const items = getItems();

        if (!items.length) {
            return;
        }

        if (e.key === "ArrowDown") {
            e.preventDefault();
            currentIndex = Math.min(currentIndex + 1, items.length - 1);
            setActive(items);
        } else if (e.key === "ArrowUp") {
            e.preventDefault();
            currentIndex = Math.max(currentIndex - 1, 0);
            setActive(items);
        } else if (e.key === "Enter") {
            if (currentIndex >= 0 && currentIndex < items.length) {
                e.preventDefault();
                items[currentIndex].click();
            }
        } else if (e.key === "Escape") {
            hideBox();
        }
    });

    document.addEventListener("click", e => {
        if (!box.contains(e.target) && e.target !== input) {
            hideBox();
        }
    });

    input.addEventListener("blur", () => {
        setTimeout(() => {
            if (!box.matches(":hover")) {
                hideBox();
            }
        }, 150);
    });
});