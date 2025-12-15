using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

[Authorize] // Require login for all post actions
public class PostController : Controller
{
    private readonly PetPetDbContext _context;

    public PostController(PetPetDbContext context)
    {
        _context = context;
    }

    // GET: Post (The Wall)
    [AllowAnonymous] // Allow guests to view
    public async Task<IActionResult> Index()
    {
        var posts = await _context.Posts
            .Include(p => p.Author)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
                .ThenInclude(c => c.Author)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return View(posts);
    }

    // GET: Post/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Post/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Content")] Post post)
    {
        if (ModelState.IsValid)
        {
            // Get current user email from claims
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            post.AuthorEmail = userEmail;
            post.CreatedAt = DateTime.Now;
            post.IsEnabled = true;

            _context.Add(post);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(post);
    }

    // POST: Post/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null) return NotFound();

        // Verify ownership
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (post.AuthorEmail != userEmail) return Forbid();

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    // POST: Post/Like/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Like(int id)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var existingLike = await _context.Likes
            .FirstOrDefaultAsync(l => l.PostId == id && l.UserEmail == userEmail);

        if (existingLike == null)
        {
            _context.Likes.Add(new Like { PostId = id, UserEmail = userEmail });
        }
        else
        {
            _context.Likes.Remove(existingLike);
        }
        
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: Post/AddComment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int postId, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return RedirectToAction(nameof(Index));

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var comment = new Comment
        {
            PostId = postId,
            UserEmail = userEmail,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
