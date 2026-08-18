using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

public class EmployeesController(ApplicationDbContext context) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        return View(await context.Employees.AsNoTracking().OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToListAsync());
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var item = await context.Employees.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View(new Employee());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Employee item)
    {
        if (!ModelState.IsValid) return View(item);
        context.Employees.Add(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await context.Employees.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Employee item)
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
        var item = await context.Employees.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await context.Employees.FindAsync(id);
        if (item is null) return NotFound();
        context.Employees.Remove(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}