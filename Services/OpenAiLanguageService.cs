using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntelligentNewsAnalyzer.Models;

namespace IntelligentNewsAnalyzer.Services;

public class OpenAiLanguageService : IAiLanguageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiLanguageService> _logger;
    private readonly string? _apiKey;
    private readonly string _model;

    public OpenAiLanguageService(HttpClient httpClient, ILogger<OpenAiLanguageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<AiAnalysisResult> AnalyzeAsync(string title, string text, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return FallbackAnalyzer.Analyze(title, text);
        }

        try
        {
            var payload = new
            {
                model = _model,
                temperature = 0.3,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = """
                        Ти си анализатор на новини. Отговори само с JSON със следните полета:
                        summary (кратко резюме на български, 2-3 изречения),
                        category (едно от: general, business, entertainment, health, science, sports, technology),
                        topics (масив от 3 до 6 кратки теми на български или английски),
                        sentiment (positive, neutral или negative).
                        """
                    },
                    new
                    {
                        role = "user",
                        content = $"Заглавие: {title}\n\nТекст:\n{text}"
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ИИ API върна {Status}: {Body}", (int)response.StatusCode, body);
                var fallback = FallbackAnalyzer.Analyze(title, text);
                fallback.Engine = "fallback-after-error";
                return fallback;
            }

            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return FallbackAnalyzer.Analyze(title, text);
            }

            var parsed = JsonSerializer.Deserialize<ModelJson>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is null)
            {
                return FallbackAnalyzer.Analyze(title, text);
            }

            var category = NewsCategories.Labels.ContainsKey(parsed.Category ?? "")
                ? parsed.Category!
                : "general";

            return new AiAnalysisResult
            {
                Summary = string.IsNullOrWhiteSpace(parsed.Summary) ? FallbackAnalyzer.Summarize(text) : parsed.Summary.Trim(),
                Category = category,
                Topics = (parsed.Topics ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Take(6).ToList(),
                Sentiment = NormalizeSentiment(parsed.Sentiment),
                Engine = "openai"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при комуникация с езиковия модел");
            var fallback = FallbackAnalyzer.Analyze(title, text);
            fallback.Engine = "fallback-after-error";
            return fallback;
        }
    }

    private static string NormalizeSentiment(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "positive" or "позитивен" => "positive",
            "negative" or "негативен" => "negative",
            _ => "neutral"
        };

    private sealed class ModelJson
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("topics")]
        public List<string>? Topics { get; set; }

        [JsonPropertyName("sentiment")]
        public string? Sentiment { get; set; }
    }
}