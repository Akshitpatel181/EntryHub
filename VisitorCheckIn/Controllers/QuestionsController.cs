using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Models;

namespace VisitorCheckIn.Controllers;

[Authorize(Roles = "Admin")]
public class QuestionsController : Controller
{
    private readonly AppDbContext _db;
    public QuestionsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Questions.OrderBy(q => q.SortOrder).ToListAsync());

    public IActionResult Create() => View("Form", new Question { IsActive = true });

    [HttpPost]
    public async Task<IActionResult> Create(Question q)
    {
        Check(q);
        if (!ModelState.IsValid) return View("Form", q);
        q.Id = 0;
        q.SortOrder = (await _db.Questions.MaxAsync(x => (int?)x.SortOrder) ?? 0) + 1;
        _db.Questions.Add(q);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Question added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var q = await _db.Questions.FindAsync(id);
        return q == null ? NotFound() : View("Form", q);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Question input)
    {
        var q = await _db.Questions.FindAsync(id);
        if (q == null) return NotFound();
        Check(input);
        if (!ModelState.IsValid) return View("Form", input);
        q.Text = input.Text; q.Type = input.Type; q.OptionsText = input.OptionsText;
        q.IsRequired = input.IsRequired; q.VisitorType = input.VisitorType; q.IsActive = input.IsActive;
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Question updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var q = await _db.Questions.FindAsync(id);
        if (q != null) { q.IsActive = !q.IsActive; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var q = await _db.Questions.FindAsync(id);
        if (q != null) { _db.Questions.Remove(q); await _db.SaveChangesAsync(); }
        TempData["Ok"] = "Question deleted (past answers are kept).";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Called by drag-and-drop (SortableJS) with the ids in their new order.</summary>
    [HttpPost]
    public async Task<IActionResult> UpdateOrder([FromBody] int[] orderedIds)
    {
        var all = await _db.Questions.ToListAsync();
        for (int i = 0; i < orderedIds.Length; i++)
        {
            var q = all.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (q != null) q.SortOrder = i;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    private void Check(Question q)
    {
        if ((q.Type == QuestionType.Dropdown || q.Type == QuestionType.Checkbox) && string.IsNullOrWhiteSpace(q.OptionsText))
            ModelState.AddModelError(nameof(q.OptionsText), "Add at least one option (one per line).");
        if (q.VisitorType != "All" && !VisitorTypes.All.Contains(q.VisitorType))
            q.VisitorType = "All";
    }
}
