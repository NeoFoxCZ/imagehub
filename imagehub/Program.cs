#region

using imagehub.Services;
using imagehub.Settings;
using imagehub.tables;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

#endregion

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddTransient<ImageService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

// Map CacheSettings model to applicationJson 
builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));

var isCacheEnabled = builder.Configuration.GetValue<bool>("CacheSettings:EnableCache");
Console.WriteLine(isCacheEnabled ? "✅Cache is enabled" : "❗️Cache is disabled");

Console.WriteLine("👀Checking images directory");
var imagesPath = Path.Combine(builder.Environment.ContentRootPath, "images");
if (!Directory.Exists(imagesPath))
{
    Console.WriteLine("❗️Images directory doesn't exist, creating it");
    Directory.CreateDirectory(imagesPath);
}
else
{
    Console.WriteLine("✅Images directory exists");
}

Console.WriteLine("📊Creating database connection (SQLite)");
builder.Services.AddDbContext<MyContext>(options =>
    options.UseSqlite("Data Source=images/app.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    Console.WriteLine("📏Applying migrations");
    var db = scope.ServiceProvider.GetRequiredService<MyContext>();
    db.Database.Migrate();
    Console.WriteLine("✅Database migrations applied");
}

Console.WriteLine("🚀Starting application");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
