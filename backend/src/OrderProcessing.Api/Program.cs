using Asp.Versioning;
using OrderProcessing.Application;
using OrderProcessing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services
    .AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.ReportApiVersions = true;
        o.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'V";
        o.SubstituteApiVersionInUrl = true;
    });
builder.Services.AddOpenApi("v1");
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
// Only needed when the Next.js dev server (port 3000) calls the API cross-origin; harmless when served same-origin.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:3000").AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.MapOpenApi();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Order Processing API v1"));
app.MapControllers();

app.Run();

public partial class Program;
