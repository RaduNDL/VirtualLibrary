using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Billing
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<CartItem> CartItems { get; set; } = new List<CartItem>();
        public decimal Total { get; set; }

        [BindProperty]
        public CheckoutInputModel Checkout { get; set; } = new CheckoutInputModel();

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            CartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!CartItems.Any())
            {
                TempData["BillingError"] = "Your cart is empty.";
                return RedirectToPage("/Cart/Index");
            }

            Total = CartItems.Sum(i => i.Product.Price * i.Quantity);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            CartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!CartItems.Any())
            {
                TempData["BillingError"] = "Your cart is empty.";
                return RedirectToPage("/Cart/Index");
            }

            Total = CartItems.Sum(i => i.Product.Price * i.Quantity);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                Status = "The product was purchased successfully.",
                TotalAmount = Total,
                Items = CartItems.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    Price = i.Product.Price
                }).ToList()
            };

            _context.Orders.Add(order);
            _context.CartItems.RemoveRange(CartItems);

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"✓ Purchase successful! You can now read your books and generate audiobooks.";
            return RedirectToPage("/MyBooks/Index");
        }
    }
}