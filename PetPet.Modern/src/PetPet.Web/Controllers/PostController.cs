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
    public IActionResult Index()
    {
        return View();
    }

    // GET: Post/GetPosts API for AJAX Loading
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetPosts(int page = 1, int pageSize = 9)
    {
        var posts = await _context.Posts
            .Include(p => p.Author)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
                .ThenInclude(c => c.Author)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new 
            {
                p.Id,
                p.Title,
                p.Content,
                p.CreatedAt,
                p.ImageUrl,
                Author = new { 
                    p.Author.Name, 
                    p.Author.Photo 
                },
                LikeCount = p.Likes.Count,
                IsLiked = User.Identity.IsAuthenticated && p.Likes.Any(l => l.UserEmail == User.FindFirstValue(ClaimTypes.Email)), // Fix IsLiked logic
                CommentCount = p.Comments.Count,
                Comments = p.Comments.Select(c => new { 
                    c.Content, 
                    AuthorName = c.Author != null ? c.Author.Name : "會員" 
                }).ToList()
            })
            .ToListAsync();

        return Json(posts);
    }

    // GET: Post/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Post/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Content,ImageUrl")] Post post)
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
        
        // Get new count
        var newCount = await _context.Likes.CountAsync(l => l.PostId == id);
        
        return Json(new { success = true, isLiked = existingLike == null, count = newCount });
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
