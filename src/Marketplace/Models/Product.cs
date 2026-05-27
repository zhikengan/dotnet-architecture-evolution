namespace Marketplace.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public Guid SellerId { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Published;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
