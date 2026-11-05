using DotNetEnv;
using IntelligentNewsAnalyzer.Data;
using IntelligentNewsAnalyzer.Services;
using Microsoft.EntityFrameworkCore;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<INewsApiService, NewsApiService>(client =>
{
    client.BaseAddress = new Uri("https://newsapi.org/v2/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IAiLanguageService, OpenAiLanguageService>(client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1/";
    if (!baseUrl.EndsWith('/'))
    {
        baseUrl += "/";
    }

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<StatisticsService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DemoData.SeedIfEmptyAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
