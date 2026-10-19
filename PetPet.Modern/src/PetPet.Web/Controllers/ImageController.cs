using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetPet.Application.Media;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

[Authorize]
public class ImageController : Controller
{
    private readonly IMediaService _mediaService;

    public ImageController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var currentEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentEmail))
        {
            return Unauthorized();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest("請選擇要上傳的圖片檔案。");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "檔案大小超過 5 MiB 限制。");
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _mediaService.UploadImageAsync(stream, file.ContentType, file.Length, currentEmail);
            return Json(new { id = result.Id, url = result.Url });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ex.Message);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "圖片上傳處理失敗。");
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Presets()
    {
        var presets = new List<string>
        {
            "/images/presets/avatar-dog.svg",
            "/images/presets/avatar-cat.svg",
            "/images/presets/avatar-boy.svg",
            "/images/presets/avatar-girl.svg"
        };
        return Json(presets);
    }
}
