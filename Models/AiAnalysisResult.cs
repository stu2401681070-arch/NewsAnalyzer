namespace IntelligentNewsAnalyzer.Models;

public class AiAnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public List<string> Topics { get; set; } = [];
    public string Sentiment { get; set; } = "neutral";
    public string Engine { get; set; } = "fallback";
}
