using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntelligentNewsAnalyzer.Services;

public static class NewsCategories
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["general"] = "Общи",
        ["business"] = "Бизнес",
        ["entertainment"] = "Забавление",
        ["health"] = "Здраве",
        ["science"] = "Наука",
        ["sports"] = "Спорт",
        ["technology"] = "Технологии"
    };

    public static string Label(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Labels["general"];
        }

        return Labels.TryGetValue(key, out var label) ? label : key;
    }

    public static IEnumerable<SelectListItem> SelectList(string? selected = null) =>
        Labels.Select(pair => new SelectListItem(pair.Value, pair.Key, pair.Key == selected));

    public static IEnumerable<SelectListItem> CountrySelectList(string? selected = "us")
    {
        var countries = new (string Code, string Name)[]
        {
            ("us", "САЩ"),
            ("gb", "Великобритания"),
            ("de", "Германия"),
            ("fr", "Франция"),
            ("it", "Италия"),
            ("bg", "България"),
            ("au", "Австралия"),
            ("ca", "Канада"),
            ("in", "Индия")
        };

        return countries.Select(c => new SelectListItem(c.Name, c.Code, c.Code == selected));
    }
}
