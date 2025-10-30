using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Suppliers
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        public List<Supplier> Suppliers { get; set; } = new();
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync(string? status = null)
        {
            StatusMessage = status;
            Suppliers = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();
        }
    }
}
