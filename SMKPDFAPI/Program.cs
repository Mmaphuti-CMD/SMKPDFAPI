using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using SMKPDFAPI.Parsing;
using SMKPDFAPI.Pdf;
using SMKPDFAPI.Swagger;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 20_000_000); // 20 MB
builder.Services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();
builder.Services.AddScoped<IStatementNormalizer, SimpleStatementNormalizer>();
builder.Services.AddScoped<ITransactionParser, RegexTransactionParser>();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SMKPDFAPI",
        Version = "v1",
        Description = "REST API that extracts transactions from Capitec Bank PDF statements using PdfPig for text extraction. Upload a PDF to receive structured JSON with dates, descriptions, amounts, fees, and balances. Uses regex pattern matching, handles multi-page statements, normalizes text, and includes Swagger UI for testing. Returns data in ZAR currency."
    });
    
    // Handle file uploads in Swagger
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
    
    // Use both operation and document filters for proper multipart/form-data handling
    c.OperationFilter<FileUploadOperationFilter>();
    c.DocumentFilter<FileUploadDocumentFilter>();
    
    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SMKPDFAPI v1");
    });
}

// Only use HTTPS redirection if HTTPS is configured
// app.UseHttpsRedirection();

app.MapControllers();

// Simple root endpoint
app.MapGet("/", () => Results.Json(new
{
    message = "SMKPDFAPI - PDF Transaction Parser API",
    info = "Visit /rootEndpoint to see all available endpoints",
    rootEndpoint = "/rootEndpoint",
    swagger = app.Environment.IsDevelopment() ? "/swagger" : null
}));

// Root endpoint - automatically discovers all endpoints
app.MapGet("/rootEndpoint", (EndpointDataSource endpointDataSource) =>
{
    var endpoints = endpointDataSource.Endpoints
        .Where(e => e is RouteEndpoint)
        .Cast<RouteEndpoint>()
        .Select(e => new
        {
            path = e.RoutePattern.RawText ?? e.RoutePattern.PathSegments
                .Select(s => s.ToString())
                .Aggregate((a, b) => a + b),
            methods = e.Metadata
                .OfType<HttpMethodMetadata>()
                .SelectMany(m => m.HttpMethods)
                .Distinct()
                .ToList()
        })
        .Where(e => e.path != null && !e.path.StartsWith("/swagger") && !e.path.StartsWith("/rootEndpoint"))
        .GroupBy(e => e.path)
        .Select(g => new
        {
            path = g.Key,
            methods = g.SelectMany(e => e.methods).Distinct().OrderBy(m => m).ToList()
        })
        .OrderBy(e => e.path)
        .ToList();

    var response = new
    {
        message = "SMKPDFAPI - PDF Transaction Parser API",
        version = "1.0",
        endpoints = endpoints.ToDictionary(
            e => e.path ?? "",
            e => (object)e.methods
        ),
        swagger = app.Environment.IsDevelopment() ? "/swagger" : null,
        openApi = app.Environment.IsDevelopment() ? "/swagger/v1/swagger.json" : null
    };

    return Results.Json(response);
});

app.Run(); 