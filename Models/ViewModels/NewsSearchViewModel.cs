using System.ComponentModel.DataAnnotations;
using IntelligentNewsAnalyzer.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntelligentNewsAnalyzer.Models.ViewModels;

public class NewsSearchViewModel
{
    [Display(Name = "Ключова дума")]
    [MaxLength(200)]
    public string? Query { get; set; }

    [Display(Name = "Категория")]
    public string? Category { get; set; }

    [Display(Name = "Държава")]
    public string Country { get; set; } = "us";

    public string? Message { get; set; }
    public bool IsError { get; set; }
    public IReadOnlyList<Article> Articles { get; set; } = [];
    public IEnumerable<SelectListItem> Categories { get; set; } = [];
    public IEnumerable<SelectListItem> Countries { get; set; } = [];
}
