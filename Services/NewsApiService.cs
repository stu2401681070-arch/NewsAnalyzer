using System.Net.Http.Json;
using IntelligentNewsAnalyzer.Data;
using IntelligentNewsAnalyzer.Models;
using IntelligentNewsAnalyzer.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace IntelligentNewsAnalyzer.Services;

public class NewsApiService : INewsApiService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly ILogger<NewsApiService> _logger;
    private readonly string? _apiKey;

    public NewsApiService(HttpClient httpClient, AppDbContext db, ILogger<NewsApiService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("NEWS_API_KEY");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<NewsFetchResult> FetchAsync(string? query, string? category, string country, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new NewsFetchResult
            {
                Success = false,
                Message = "Липсва NEWS_API_KEY в .env файла. Добавете ключ от https://newsapi.org и рестартирайте приложението."
            };
        }

        try
        {
            var requestUri = BuildRequestUri(query, category, country);
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);
            request.Headers.TryAddWithoutValidation("User-Agent", "IntelligentNewsAnalyzer/1.0");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<NewsApiResponse>(cancellationToken);

            if (payload is null)
            {
                return new NewsFetchResult { Success = false, Message = "News API върна празен отговор." };
            }

            if (!response.IsSuccessStatusCode || !string.Equals(payload.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                var error = payload.Message ?? $"HTTP {(int)response.StatusCode}";
                return new NewsFetchResult { Success = false, Message = $"News API: {error}" };
            }

            var saved = await SaveArticlesAsync(payload.Articles, category, cancellationToken);
            return new NewsFetchResult
            {
                Success = true,
                Message = saved.Count == 0
                    ? "Няма нови статии за запис (възможно е вече да са в базата)."
                    : $"Записани са {saved.Count} новини от външния API.",
                Articles = saved
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при извличане от News API");
            return new NewsFetchResult { Success = false, Message = $"Неуспешна връзка с News API: {ex.Message}" };
        }
    }

    private static string BuildRequestUri(string? query, string? category, string country)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = Uri.EscapeDataString(query.Trim());
            var extra = string.IsNullOrWhiteSpace(category) ? string.Empty : $"&searchIn=title,description";
            return $"everything?q={q}&language=en&sortBy=publishedAt&pageSize=20{extra}";
        }

        var cat = string.IsNullOrWhiteSpace(category) ? "general" : category;
        var cc = string.IsNullOrWhiteSpace(country) ? "us" : country;
        return $"top-headlines?country={Uri.EscapeDataString(cc)}&category={Uri.EscapeDataString(cat)}&pageSize=20";
    }

    private async Task<List<Article>> SaveArticlesAsync(IEnumerable<NewsApiArticle> incoming, string? category, CancellationToken cancellationToken)
    {
        var saved = new List<Article>();
        var fallbackCategory = string.IsNullOrWhiteSpace(category) ? "general" : category;

        foreach (var item in incoming)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Url) || item.Title.Equals("[Removed]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var exists = await _db.Articles.AnyAsync(a => a.Url == item.Url, cancellationToken);
            if (exists)
            {
                continue;
            }

            var article = new Article
            {
                Title = item.Title.Trim(),
                Description = item.Description,
                Content = item.Content,
                Url = item.Url,
                ImageUrl = item.UrlToImage,
                SourceName = string.IsNullOrWhiteSpace(item.Source?.Name) ? "Неизвестен източник" : item.Source!.Name!,
                Author = item.Author,
                Category = fallbackCategory,
                PublishedAt = item.PublishedAt?.ToUniversalTime(),
                FetchedAt = DateTime.UtcNow
            };

            _db.Articles.Add(article);
            saved.Add(article);
        }

        if (saved.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return saved;
    }
}
