using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

public class ContactMessagesController(ApplicationDbContext context) : Controller
{
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        return View(await context.ContactMessages.AsNoTracking().OrderByDescending(c => c.SentAt).ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Details(int id)
    {
        var item = await context.ContactMessages.FirstOrDefaultAsync(c => c.Id == id);
        return item is null ? NotFound() : View(item);
    }

    [AllowAnonymous]
    public IActionResult Create() => View(new ContactMessage());

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContactMessage item)
    {
        if (!ModelState.IsValid) return View(item);

        item.Id = 0;
        item.IsRead = false;
        item.SentAt = DateTime.UtcNow;
        context.ContactMessages.Add(item);
        await context.SaveChangesAsync();
        TempData["ContactSubmitted"] = "Your message has been sent.";
        return RedirectToAction(nameof(Create));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var item = await context.ContactMessages.FindAsync(id);
        if (item is null) return NotFound();
        item.IsRead = true;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await context.ContactMessages.FirstOrDefaultAsync(c => c.Id == id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await context.ContactMessages.FindAsync(id);
        if (item is null) return NotFound();
        context.ContactMessages.Remove(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}