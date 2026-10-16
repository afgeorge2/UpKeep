using Microsoft.EntityFrameworkCore;
using UpKeep.Data;
using UpKeep.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using UpKeep.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddUpKeepSignIn();

builder.Services.AddDbContext<UpKeepDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("UpKeepDatabase");

    options.UseSqlServer(connectionString);
});

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpClient<EquipmentAnalysis>(client => client.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("equipment", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

// Serve the built browser app on the API's HTTPS origin for secure same-origin sessions.
var browserRoot = app.Environment.IsDevelopment()
    ? Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "../UpKeepClient/dist"))
    : Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(browserRoot))
{
    var files = new PhysicalFileProvider(browserRoot);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files }).AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();

app.Run();

public partial class Program;

