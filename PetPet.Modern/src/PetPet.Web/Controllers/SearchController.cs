using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Social;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

public class SearchViewModel
{
    public string? Query { get; set; }
    public int? CityId { get; set; }
    public string Sort { get; set; } = "latest"; // "latest", "likes"
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
    public List<PostFeedItemDto> Results { get; set; } = [];
}

[AllowAnonymous]
public class SearchController : Controller
{
    private readonly PetPetDbContext _context;

    public SearchController(PetPetDbContext context)
    {
        _context = context;
    }

    // GET: /Search
    [HttpGet]
    public async Task<IActionResult> Index(string? q, int? cityId, string sort = "latest", int page = 1)
    {
        if (page < 1) page = 1;
        const int pageSize = 20;

        string? trimmedQ = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (trimmedQ?.Length > 100) trimmedQ = trimmedQ.Substring(0, 100);

        var viewerEmail = User.FindFirstValue(ClaimTypes.Email);

        // Blocks check
        var blockedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (viewerEmail != null)
        {
            var blocked = await _context.Blocks.AsNoTracking()
                .Where(b => b.BlockerEmail == viewerEmail || b.BlockedEmail == viewerEmail)
                .Select(b => b.BlockerEmail == viewerEmail ? b.BlockedEmail : b.BlockerEmail)
                .ToListAsync();
            foreach (var b in blocked) blockedEmails.Add(b);
        }

        var query = _context.Posts.AsNoTracking()
            .Include(p => p.Author)
            .Where(p => p.IsEnabled && !p.IsDeleted);

        if (blockedEmails.Any())
        {
            query = query.Where(p => !blockedEmails.Contains(p.AuthorEmail));
        }

        if (!string.IsNullOrEmpty(trimmedQ))
        {
            // Literal contains
            query = query.Where(p => p.Title.Contains(trimmedQ) || p.Content.Contains(trimmedQ));
        }

        if (cityId.HasValue && cityId.Value > 0)
        {
            query = query.Where(p => p.Author.CityId == cityId.Value);
        }

        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var orderedQuery = sort.ToLowerInvariant() switch
        {
            "likes" => query.OrderByDescending(p => p.Likes.Count).ThenByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostFeedItemDto
            {
                Id = p.Id,
                Title = p.Title,
                Snippet = p.Content.Length > 160 ? p.Content.Substring(0, 160) + "..." : p.Content,
                ImageUrl = p.ImageUrl,
                CreatedAt = p.CreatedAt,
                AuthorName = p.Author.Name,
                AuthorPublicId = p.Author.PublicId,
                AuthorPhoto = p.Author.Photo,
                LikeCount = p.Likes.Count(),
                CommentCount = p.Comments.Count(c => !c.IsDeleted),
                ViewerHasLiked = viewerEmail != null && p.Likes.Any(l => l.UserEmail == viewerEmail)
            })
            .ToListAsync();

        ViewBag.Cities = await _context.Cities.AsNoTracking().OrderBy(c => c.Id).ToListAsync();

        var model = new SearchViewModel
        {
            Query = trimmedQ,
            CityId = cityId,
            Sort = sort.ToLowerInvariant() == "likes" ? "likes" : "latest",
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            Results = items
        };

        return View(model);
    }
}
