using Soundsentiment.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// CORS: allow local frontend during development
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5500", "http://127.0.0.1:5500", "http://localhost:5257")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers()
       .AddJsonOptions(opts =>
       {
           // Use camelCase so frontend (JavaScript) can access properties as e.g. data.careerRisk
           opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
       });

// Servicios externos
builder.Services.AddHttpClient<AudioDbService>();
builder.Services.AddHttpClient<HuggingFaceService>();

// Servicio de análisis
builder.Services.AddScoped<AnalysisService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve static files from wwwroot (index.html, app.js, styles.css)
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable CORS policy (useful if frontend is served separately during development)
app.UseCors("LocalDevPolicy");

app.MapControllers();

app.Run();
