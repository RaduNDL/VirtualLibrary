document.addEventListener("DOMContentLoaded", function () {
    const searchForm = document.getElementById("searchForm");
    const searchButton = document.getElementById("searchButton");
    const sortSelect = document.getElementById("sortSelect");
    const searchInput = document.getElementById("searchInputGroup");

    if (searchForm) {
        searchForm.addEventListener("submit", function () {
            if (searchButton) {
                searchButton.disabled = true;
                searchButton.innerHTML = "<i class='bx bx-loader-alt bx-spin vt-btn-icon'></i> <span>Searching...</span>";
                searchButton.classList.add("btn-loading");
            }
        });
    }

    if (sortSelect && searchInput) {
        sortSelect.addEventListener("change", function () {
            if (searchInput.value.trim() !== "") {
                searchForm.submit();
            }
        });
    }
});