namespace ApiGateway.Models;

public class Label
{
    public int LabelId { get; set; }

    public required string Code { get; set; }

    public required string CustomerLabel { get; set; }

    public required string CollaboratorLabel { get; set; }

    public required string Business { get; set; }

    public string? Description { get; set; }

    public required bool IsVisible { get; set; } = true;
}
