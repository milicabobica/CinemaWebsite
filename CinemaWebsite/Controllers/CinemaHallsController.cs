using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

public class CinemaHallsController(ApplicationDbContext context) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index() => View(await context.CinemaHalls.AsNoTracking().OrderBy(h => h.Number).ToListAsync());

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var item = await context.CinemaHalls.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View(new CinemaHall());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CinemaHall item)
    {
        if (!ModelState.IsValid) return View(item);
        context.CinemaHalls.Add(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await context.CinemaHalls.FindAsync(id);
        if (item is null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CinemaHall item)
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
        var item = await context.CinemaHalls
            .Include(h => h.Screenings)
            .FirstOrDefaultAsync(h => h.Id == id);
        if (item is null)
        {
            return NotFound();
        }
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await context.CinemaHalls
            .Include(h => h.Screenings)
            .FirstOrDefaultAsync(h => h.Id == id);
        if (item is null) return NotFound();
        if (item.Screenings.Any())
        {
            ModelState.AddModelError(string.Empty, "Cannot delete hall with existing screenings.");
            return View("Delete", item);
        }

        context.CinemaHalls.Remove(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}