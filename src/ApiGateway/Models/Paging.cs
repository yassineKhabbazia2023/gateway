namespace ApiGateway.Models;

public class Paging<T>
{
    public List<T> Items { get; set; } = new List<T>();

    public int CurrentPage { get; set; }

    public int TotalPage { get; set; }

    public int TotalItems { get; set; }
}