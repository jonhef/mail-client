using MailClient.Server.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var allowAnyOrigin = builder.Environment.IsDevelopment();
if (builder.Environment.IsProduction() && allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("AllowedOrigins must be configured in production.");
}

builder.Services.AddCors(opts =>
{
    opts.AddPolicy("cors", p =>
    {
        if (allowAnyOrigin)
        {
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            p.WithOrigins(allowedOrigins)
             .AllowAnyHeader()
             .AllowAnyMethod();
        }
    });
});

builder.Services.AddSingleton<CryptoService>();
builder.Services.AddSingleton<AccountStore>();
builder.Services.AddSingleton<AutodiscoverService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GoogleOAuthService>();
builder.Services.AddSingleton<MailService>();

var dataProtectionPath = builder.Configuration.GetValue<string>("DataProtectionKeysPath")
                       ?? builder.Configuration.GetValue<string>("DataProtection:KeysPath")
                       ?? "Data/keys";
var fullKeysPath = Path.Combine(builder.Environment.ContentRootPath, dataProtectionPath);
Directory.CreateDirectory(fullKeysPath);
var dataProtectionLifetimeDays = builder.Configuration.GetValue<int?>("DataProtection:KeyLifetimeDays");
var dataProtection = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(fullKeysPath))
    .SetApplicationName("MailClient");

if (dataProtectionLifetimeDays.HasValue && dataProtectionLifetimeDays.Value > 0)
{
    dataProtection.SetDefaultKeyLifetime(TimeSpan.FromDays(dataProtectionLifetimeDays.Value));
}

var app = builder.Build();

app.UseRouting();
app.UseCors("cors");
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
