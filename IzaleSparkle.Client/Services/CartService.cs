using IzaleSparkle.Client.Models;

namespace IzaleSparkle.Client.Services;

public class CartService
{
    private readonly List<CartItem> _items = new();

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
    public int Count => _items.Sum(i => i.Quantity);
    public decimal Subtotal => _items.Sum(i => i.LineTotal);
    public decimal Vat => Math.Round(_items.Sum(i => i.Product.IsVatApplicable ? i.LineTotal * 0.20m : 0m), 2);
    public decimal Shipping { get; private set; } = 0;
    public decimal Total => Subtotal + Shipping + Vat;

    public bool IsEmpty => _items.Count == 0;

    public event Action? OnChange;

    public void AddItem(Product product, int qty = 1, string metal = "18K White Gold", string? size = null)
    {
        var existing = _items.FirstOrDefault(i => i.Product.Id == product.Id && i.SelectedMetal == metal && i.SelectedSize == size);
        if (existing != null)
            existing.Quantity += qty;
        else
            _items.Add(new CartItem { Product = product, Quantity = qty, SelectedMetal = metal, SelectedSize = size });
        NotifyChange();
    }

    public void RemoveItem(int productId)
    {
        var item = _items.FirstOrDefault(i => i.Product.Id == productId);
        if (item != null) { _items.Remove(item); NotifyChange(); }
    }

    public void UpdateQuantity(int productId, int qty)
    {
        var item = _items.FirstOrDefault(i => i.Product.Id == productId);
        if (item == null) return;
        if (qty <= 0) _items.Remove(item);
        else item.Quantity = qty;
        NotifyChange();
    }

    public void SetShipping(decimal cost) { Shipping = cost; NotifyChange(); }

    public void Clear() { _items.Clear(); NotifyChange(); }

    private void NotifyChange() => OnChange?.Invoke();
}
