using InvoiceWizard.Web.Components;
using InvoiceWizard.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var baseUrl = builder.Configuration["Backend:BaseUrl"] ?? "https://localhost:7216/";

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<WebAuthSession>();
builder.Services.AddScoped<WebUiSelectionState>();
builder.Services.AddScoped<WebOfflineDataService>();
builder.Services.AddScoped<AuthHeaderHandler>();
builder.Services.AddHttpClient("BackendAnonymous", client =>
{
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddHttpClient<BackendApiClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


