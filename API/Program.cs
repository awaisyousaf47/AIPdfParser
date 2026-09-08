using Infrastructure.Configuration;
using Infrastructure.LLM;
using Infrastructure.PdfProcessing;
using Infrastructure.VectorDB;
using Services.Implementations;
using Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure options (with validation on startup)
builder.Services.AddOptions<QdrantOptions>()
    .Bind(builder.Configuration.GetSection(nameof(QdrantOptions)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<OpenAIOptions>()
    .Bind(builder.Configuration.GetSection(nameof(OpenAIOptions)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register services - Singleton for QdrantStore (thread-safe client wrapper)
builder.Services.AddSingleton<IVectorStore, QdrantStore>();
builder.Services.AddScoped<IPdfProcessor, PdfPigProcessor>();

// OpenAIService implements both interfaces - register the concrete type once,
// then forward both interfaces to the SAME instance per scope.
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<OpenAIService>());
builder.Services.AddScoped<ILLMService>(sp => sp.GetRequiredService<OpenAIService>());

builder.Services.AddScoped<IRagService, RagService>();

// Add health checks
builder.Services.AddHealthChecks();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();