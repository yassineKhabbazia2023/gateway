using SampleOcelotDemoApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();
app.MapGet("/ged-social", () =>
    {

        return new GedDocumentsResponse().Documents;
    })
    .WithName("GetGed")
    .WithOpenApi();
app.MapHealthChecks("healthz");
app.Run();

namespace SampleOcelotDemoApi
{
    record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
    {
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
    public class GedDocument
    {
        public string DocumentId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public DateTime DateCreated { get; set; }
        public string Summary { get; set; }
        public string FileUrl { get; set; }

        // Constructor to pre-initialize the GedDocument
        public GedDocument()
        {
            DocumentId = "defaultDocId";
            Title = "Default Title";
            Author = "Default Author";
            DateCreated = DateTime.Now;
            Summary = "Default summary of the document.";
            FileUrl = "http://example.com/defaultfile.pdf";
        }
    }

    public class GedDocumentsResponse
    {
        public List<GedDocument> Documents { get; set; } = new();
    
        // Constructor to pre-initialize with some documents
        public GedDocumentsResponse()
        {
            Documents.Add(new GedDocument
            {
                DocumentId = "doc123",
                Title = "Document Title 1",
                Author = "Author Name 1",
                DateCreated = new DateTime(2024, 1, 1),
                Summary = "Summary of Document 1",
                FileUrl = "http://example.com/files/doc123.pdf"
            });

            // Add more pre-initialized documents as needed
        }
    }
}