using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using RecouvrementAPI.Data;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- CONFIGURATION QUESTPDF ---
QuestPDF.Settings.License = LicenseType.Community;

// --- CONNECTION STRING ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// --- DBCONTEXT avec MySQL officiel (Oracle) ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(connectionString!));

// --- JWT AUTHENTICATION ---
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey!))
        };
    });

builder.Services.AddAuthorization();

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// --- PIPELINE ---
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "L'API est en ligne sur .NET 10 !");
app.MapControllers();

await app.RunAsync();

public partial class Program 
{
    protected Program() { }
}