using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

public class NewsController(ApplicationDbContext context) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var query = context.News.AsQueryable();
        if (!User.IsInRole("Admin"))
        {
            query = query.Where(n => n.IsPublished);
        }

        var items = await query.OrderByDescending(n => n.PublishedDate).ToListAsync();
        return View(items);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var item = await context.News.FirstOrDefaultAsync(n => n.Id == id);
        if (item is null) return NotFound();
        if (!item.IsPublished && !User.IsInRole("Admin")) return NotFound();
        return View(item);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View(new News { PublishedDate = DateTime.UtcNow });

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(News item)
    {
        if (!ModelState.IsValid) return View(item);

        context.News.Add(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await context.News.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, News item)
    {
        if (id != item.Id) return BadRequest();
        if (!ModelState.IsValid) return View(item);
        context.Entry(item).State = EntityState.Modified;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await context.News.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await context.News.FindAsync(id);
        if (item is null) return NotFound();
        context.News.Remove(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}