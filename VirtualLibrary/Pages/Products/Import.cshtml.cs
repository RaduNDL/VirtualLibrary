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
        public ImportModel(BookImporter importer) => _importer = importer;

        [BindProperty] public string Subject { get; set; } = "fiction";
        [BindProperty] public int Count { get; set; } = 60;

        public string? Status { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Count <= 0) Count = 20;
            var n = await _importer.ImportGoogleBooksAsync(Subject, Count);
            TempData["StatusMessage"] = $"Imported {n} book(s) for subject '{Subject}'.";
            return RedirectToPage("./Index");
        }
    }
}
