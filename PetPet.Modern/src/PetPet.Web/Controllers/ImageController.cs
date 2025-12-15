using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PetPet.Web.Controllers
{
    [Authorize]
    public class ImageController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public ImageController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (file.Length > 5 * 1024 * 1024) // 5MB
                return BadRequest("File size exceeds 5MB limit.");

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif")
                return BadRequest("Only images (jpg, png, gif) are allowed.");

            // Create Directory
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            // Save File
            var fileName = Guid.NewGuid().ToString() + ext;
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Json(new { url = $"/uploads/{fileName}" });
        }

        [HttpGet]
        public IActionResult Presets()
        {
            // For MVP, return hardcoded list of generated SVGs
            // In a real app, scan the directory
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
}
