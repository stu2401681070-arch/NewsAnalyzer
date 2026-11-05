using System.Text.RegularExpressions;
using IntelligentNewsAnalyzer.Models;

namespace IntelligentNewsAnalyzer.Services;

public static class FallbackAnalyzer
{
    private static readonly Dictionary<string, string[]> CategoryKeywords = new()
    {
        ["technology"] = ["ai", "tech", "software", "computer", "chip", "digital", "internet", "robot", "app", "cyber", "data"],
        ["business"] = ["market", "stock", "bank", "economy", "inflation", "trade", "company", "ceo", "finance", "invest"],
        ["health"] = ["health", "hospital", "doctor", "virus", "vaccine", "medical", "drug", "patient", "who"],
        ["science"] = ["nasa", "space", "research", "scientist", "climate", "physics", "biology", "study"],
        ["sports"] = ["match", "league", "goal", "olympic", "football", "soccer", "nba", "tennis", "coach", "cup"],
        ["entertainment"] = ["movie", "film", "music", "actor", "netflix", "festival", "celebrity", "album", "tv"]
    };

    public static AiAnalysisResult Analyze(string title, string text)
    {
        var combined = $"{title} {text}";
        return new AiAnalysisResult
        {
            Summary = Summarize(text),
            Category = Classify(combined),
            Topics = ExtractTopics(combined),
            Sentiment = DetectSentiment(combined),
            Engine = "fallback"
        };
    }

    public static string Summarize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Няма достатъчно текст за резюме.";
        }

        var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+")
            .Where(s => s.Trim().Length > 20)
            .Take(3)
            .ToArray();

        return sentences.Length == 0
            ? text.Trim()[..Math.Min(text.Trim().Length, 280)]
            : string.Join(" ", sentences);
    }

    private static string Classify(string text)
    {
        var lower = text.ToLowerInvariant();
        var best = "general";
        var bestScore = 0;

        foreach (var (category, keywords) in CategoryKeywords)
        {
            var score = keywords.Count(k => lower.Contains(k));
            if (score > bestScore)
            {
                bestScore = score;
                best = category;
            }
        }

        return best;
    }

    private static List<string> ExtractTopics(string text)
    {
        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the","and","for","with","that","this","from","have","will","are","was","were","been","not","but",
            "you","your","our","their","they","his","her","its","about","after","over","into","than","then",
            "said","says","just","more","also","can","has","had","who","what","when","where","which"
        };

        return Regex.Matches(text.ToLowerInvariant(), @"[a-zа-яё]{4,}")
            .Select(m => m.Value)
            .Where(w => !stop.Contains(w))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => char.ToUpper(g.Key[0]) + g.Key[1..])
            .ToList();
    }

    private static string DetectSentiment(string text)
    {
        var lower = text.ToLowerInvariant();
        var positive = new[] { "win", "growth", "success", "record", "boost", "agree", "peace", "improve" };
        var negative = new[] { "crisis", "death", "kill", "war", "crash", "loss", "fail", "attack", "risk" };
        var pos = positive.Count(w => lower.Contains(w));
        var neg = negative.Count(w => lower.Contains(w));
        if (pos > neg) return "positive";
        if (neg > pos) return "negative";
        return "neutral";
    }
}
