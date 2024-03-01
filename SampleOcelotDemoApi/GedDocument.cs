namespace SampleOcelotDemoApi;

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
        DateCreated = DateTime.UtcNow;
        Summary = "Default summary of the document.";
        FileUrl = "http://example.com/defaultfile.pdf";
    }
}