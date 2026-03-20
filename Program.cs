var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.Configure<RouteAnalyzer.Options.RouteAnalyzerOptions>(
    builder.Configuration.GetSection(RouteAnalyzer.Options.RouteAnalyzerOptions.SectionName));
builder.Services.AddHttpClient<RouteAnalyzer.Services.IpGeoLookupService>(client =>
{
    client.BaseAddress = new Uri("https://ipwho.is/");
    client.Timeout = TimeSpan.FromSeconds(2.5);
});
builder.Services.AddSingleton<RouteAnalyzer.Services.NetworkRouteDiagnosticService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
