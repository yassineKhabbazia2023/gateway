namespace SampleOcelotDemoApi;

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