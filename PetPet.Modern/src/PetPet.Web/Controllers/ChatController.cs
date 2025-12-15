using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Infrastructure.Data;
using System.Security.Claims;

namespace PetPet.Web.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly PetPetDbContext _context;

        public ChatController(PetPetDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? with)
        {
            var currentEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentEmail)) return RedirectToAction("Login", "Member");

            // 1. Get List of Recent Contacts
            var contactEmails = await _context.Messages
                .Where(m => m.SenderEmail == currentEmail || m.ReceiverEmail == currentEmail)
                .Select(m => m.SenderEmail == currentEmail ? m.ReceiverEmail : m.SenderEmail)
                .Distinct()
                .ToListAsync();

            if (!string.IsNullOrEmpty(with) && !contactEmails.Contains(with))
            {
                contactEmails.Add(with);
            }

            var contacts = await _context.Members
                .Where(m => contactEmails.Contains(m.Email))
                .ToListAsync();

            ViewBag.Contacts = contacts;
            ViewBag.CurrentEmail = currentEmail;
            ViewBag.TargetEmail = with;

            // 2. If 'with' is selected, load history
            if (!string.IsNullOrEmpty(with))
            {
                var history = await _context.Messages
                    .Where(m => (m.SenderEmail == currentEmail && m.ReceiverEmail == with) || 
                                (m.SenderEmail == with && m.ReceiverEmail == currentEmail))
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();
                
                var target = await _context.Members.FirstOrDefaultAsync(m => m.Email == with);
                ViewBag.TargetName = target?.Name ?? "Unknown";

                return View(history);
            }

            return View(new List<PetPet.Domain.Entities.Message>());
        }
    }
}
