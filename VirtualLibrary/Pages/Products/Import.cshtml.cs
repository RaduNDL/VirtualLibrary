using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.Products
{
    [Authorize(Roles = "Administrator")]
    public class ImportModel : PageModel
    {
        private readonly BookImporter _importer;
        private readonly ILogger<ImportModel> _logger;

        public ImportModel(BookImporter importer, ILogger<ImportModel> logger)
        {
            _importer = importer;
            _logger = logger;
        }

        [BindProperty] public string Subject { get; set; } = "fiction";
        [BindProperty] public int Count { get; set; } = 60;

        public string? Status { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (Count <= 0) Count = 20;
                if (Count > 400) Count = 400;

                _logger.LogInformation("Import requested: subject='{Subject}', count={Count}", Subject, Count);

                var n = await _importer.ImportGoogleBooksAsync(Subject, Count);

                if (n > 0)
                {
                    TempData["StatusMessage"] = $"✅ Successfully imported {n} book(s) for subject '{Subject}'. PDFs are being searched automatically in the background.";
                }
                else
                {
                    TempData["StatusMessage"] = $"⚠️ No books were imported for subject '{Subject}'. The Google Books API may be rate-limited. Try again in a few minutes or try a different subject.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import failed for subject '{Subject}'", Subject);
                TempData["StatusMessage"] = $"❌ Import failed: {ex.Message}";
            }

            return RedirectToPage("./Index");
        }
    }
}