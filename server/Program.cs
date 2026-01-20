using MailClient.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("cors", p =>
    {
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<CryptoService>();
builder.Services.AddSingleton<AccountStore>();
builder.Services.AddSingleton<AutodiscoverService>();
builder.Services.AddSingleton<MailService>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath,
            builder.Configuration.GetValue<string>("DataProtectionKeysPath") ?? "Data/keys")))
    .SetApplicationName("MailClient");

var app = builder.Build();

app.UseCors("cors");
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
