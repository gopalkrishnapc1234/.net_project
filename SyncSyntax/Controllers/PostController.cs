using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SyncSyntax.Data;
using SyncSyntax.Models;
using SyncSyntax.Models.ViewModels;

namespace SyncSyntax.Controllers
{
    [Authorize] // ✅ All logged-in users allowed by default
    public class PostController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png" };

        public PostController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ================= CREATE =================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            var vm = new PostViewModel
            {
                Categories = new SelectList(_context.Categories, "Id", "Name")
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            if (vm.FeatureImage != null)
            {
                var ext = Path.GetExtension(vm.FeatureImage.FileName).ToLower();

                if (!_allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("", "Only jpg, jpeg, png allowed");
                    return View(vm);
                }

                vm.Post.FeatureImagePath = await UploadImage(vm.FeatureImage);
            }

            _context.Posts.Add(vm.Post);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // ================= EDIT =================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = new PostViewModel
            {
                Categories = new SelectList(_context.Categories, "Id", "Name"),
                Post = await _context.Posts.FindAsync(id)
            };

            if (vm.Post == null)
                return NotFound();

            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PostViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var postDb = await _context.Posts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == vm.Post.Id);

            if (postDb == null)
                return NotFound();

            if (vm.FeatureImage != null)
            {
                var ext = Path.GetExtension(vm.FeatureImage.FileName).ToLower();

                if (!_allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("", "Invalid image format");
                    return View(vm);
                }

                if (!string.IsNullOrEmpty(postDb.FeatureImagePath))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, "images",
                        Path.GetFileName(postDb.FeatureImagePath));

                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                vm.Post.FeatureImagePath = await UploadImage(vm.FeatureImage);
            }
            else
            {
                vm.Post.FeatureImagePath = postDb.FeatureImagePath;
            }

            _context.Posts.Update(vm.Post);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // ================= LIST =================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Index(int? categoryId)
        {
            var posts = _context.Posts
                .Include(p => p.Category)
                .AsQueryable();

            if (categoryId.HasValue)
                posts = posts.Where(p => p.CategoryId == categoryId);

            ViewData["Categories"] = _context.Categories.ToList();

            return View(posts.ToList());
        }

        // ================= DETAIL =================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Detail(int id)
        {
            var post = _context.Posts
                .Include(p => p.Category)
                .Include(p => p.Comments)
                .FirstOrDefault(p => p.Id == id);

            if (post == null)
                return NotFound();

            return View(post);
        }

        // ================= DELETE =================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return NotFound();

            return View(post);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirm(int id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return NotFound();

            if (!string.IsNullOrEmpty(post.FeatureImagePath))
            {
                var path = Path.Combine(_env.WebRootPath, "images",
                    Path.GetFileName(post.FeatureImagePath));

                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // ================= COMMENT SYSTEM =================
        [HttpPost]
        [Authorize] // ✅ any logged-in user
        public JsonResult AddComment([FromBody] Comment comment)
        {
            if (comment == null || string.IsNullOrEmpty(comment.Content) || comment.PostId == 0)
            {
                return Json(new { success = false, message = "Invalid data" });
            }

            try
            {
                comment.UserName = User.Identity.Name;
                comment.CommentDate = DateTime.UtcNow;

                _context.Comments.Add(comment);
                _context.SaveChanges();

                return Json(new
                {
                    success = true,
                    userName = comment.UserName,
                    commentDate = comment.CommentDate.ToString("MMMM dd, yyyy"),
                    content = comment.Content
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ================= IMAGE UPLOAD =================
        private async Task<string> UploadImage(IFormFile file)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var folder = Path.Combine(_env.WebRootPath, "images");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/images/" + fileName;
        }
    }
}