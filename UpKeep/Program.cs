using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UpKeep.Data;
using UpKeep.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUpKeepAuthentication(
    builder.Configuration,
    builder.Environment);

builder.Services.AddDbContext<UpKeepDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("UpKeepDatabase");

    options.UseSqlServer(connectionString);
});

// Add services to the container.

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

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

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var isProtectedApi =
        context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/auth");

    if (isProtectedApi &&
        context.User.Identity?.IsAuthenticated == true)
    {
        var workspaceAccess = context.RequestServices
            .GetRequiredService<WorkspaceAccess>();
        await workspaceAccess.GetPersonalWorkspaceId();
    }

    await next(context);
});

app.MapControllers();

app.Run();

public partial class Program;
