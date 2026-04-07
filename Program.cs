using RecycleBinWeb.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<RecycleBinService>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

var url = "http://localhost:5051";
app.Lifetime.ApplicationStarted.Register(() =>
{
    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
    catch { }
});

app.Run(url);
