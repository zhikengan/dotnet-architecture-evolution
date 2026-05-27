using Marketplace.Domain.Products;

namespace Marketplace.Application.Tests.Common.Builders;

public sealed class ProductBuilder
{
    private string _name = "Sample Product";
    private decimal _price = 10m;
    private int _stock = 100;
    private Guid _sellerId = new("11111111-1111-1111-1111-111111111111");
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private bool _suspend;

    public ProductBuilder WithName(string name) { _name = name; return this; }
    public ProductBuilder WithPrice(decimal price) { _price = price; return this; }
    public ProductBuilder WithStock(int stock) { _stock = stock; return this; }
    public ProductBuilder WithSeller(Guid seller) { _sellerId = seller; return this; }
    public ProductBuilder At(DateTime now) { _now = now; return this; }
    public ProductBuilder Suspended() { _suspend = true; return this; }

    public Product Build()
    {
        var product = Product.Create(_name, Money.Usd(_price), _stock, _sellerId, _now).Value;
        if (_suspend) product.Suspend();
        product.ClearDomainEvents();
        return product;
    }
}
