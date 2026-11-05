using IntelligentNewsAnalyzer.Data;
using IntelligentNewsAnalyzer.Models;
using IntelligentNewsAnalyzer.Models.ViewModels;
using IntelligentNewsAnalyzer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelligentNewsAnalyzer.Controllers;

public class NewsController : Controller
{
    private readonly AppDbContext _db;
    private readonly INewsApiService _newsApi;
    private readonly AnalysisService _analysis;

    public NewsController(AppDbContext db, INewsApiService newsApi, AnalysisService analysis)
    {
        _db = db;
        _newsApi = newsApi;
        _analysis = analysis;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? query, string? category, string country = "us", CancellationToken cancellationToken = default)
    {
        var articlesQuery = _db.Articles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            articlesQuery = articlesQuery.Where(a =>
                a.Title.Contains(query) ||
                (a.Description != null && a.Description.Contains(query)) ||
                a.SourceName.Contains(query));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            articlesQuery = articlesQuery.Where(a => a.Category == category);
        }

        var articles = await articlesQuery
            .OrderByDescending(a => a.PublishedAt ?? a.FetchedAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        var message = TempData["Status"] as string;
        var isError = TempData["IsError"] as string == "1";
        return View(BuildViewModel(query, category, country, articles, message, isError));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fetch(NewsSearchViewModel form, CancellationToken cancellationToken)
    {
        var result = await _newsApi.FetchAsync(form.Query, form.Category, form.Country, cancellationToken);

        _db.SearchHistory.Add(new SearchHistory
        {
            Query = form.Query ?? string.Empty,
            Category = form.Category,
            Country = form.Country,
            ResultCount = result.Articles.Count,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = result.Message;
        TempData["IsError"] = result.Success ? "0" : "1";
        return RedirectToAction(nameof(Index), new { query = form.Query, category = form.Category, country = form.Country });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var article = await _db.Articles
            .Include(a => a.Analyses)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article is null)
        {
            return NotFound();
        }

        return View(article);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze(int id, CancellationToken cancellationToken)
    {
        var analysis = await _analysis.AnalyzeArticleAsync(id, cancellationToken);
        if (analysis is null)
        {
            return NotFound();
        }

        TempData["Status"] = "Анализът е готов.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static NewsSearchViewModel BuildViewModel(
        string? query,
        string? category,
        string country,
        IReadOnlyList<Models.Article> articles,
        string? message,
        bool isError)
    {
        return new NewsSearchViewModel
        {
            Query = query,
            Category = category,
            Country = country,
            Articles = articles,
            Message = message,
            IsError = isError,
            Categories = NewsCategories.SelectList(category),
            Countries = NewsCategories.CountrySelectList(country)
        };
    }
}
