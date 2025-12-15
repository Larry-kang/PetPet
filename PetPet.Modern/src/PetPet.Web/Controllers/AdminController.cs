using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly PetPetDbContext _context;

        public AdminController(PetPetDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.UserCount = await _context.Members.CountAsync();
            ViewBag.PostCount = await _context.Posts.CountAsync();
            ViewBag.ReportCount = await _context.Reports.CountAsync();
            ViewBag.NewsCount = await _context.News.CountAsync();

            return View();
        }

        public async Task<IActionResult> ManageUsers()
        {
            var users = await _context.Members.OrderBy(m => m.Email).ToListAsync();
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUser(string email)
        {
            var user = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);
            if (user != null && !user.IsAdmin) // Prevent disabling admin
            {
                user.IsEnabled = !user.IsEnabled;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageUsers));
        }

        public async Task<IActionResult> ManageReports()
        {
            var reports = await _context.Reports.OrderByDescending(r => r.CreatedAt).ToListAsync();
            return View(reports);
        }
    }
}
