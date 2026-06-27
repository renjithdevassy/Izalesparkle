using IzaleSparkle.Client.Models;

namespace IzaleSparkle.Client.Services;

public class CartService
{
    private readonly List<CartItem> _items = new();
    private bool _suppressChangeEvent = false;

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
    public int Count => _items.Sum(i => i.Quantity);
    public decimal Subtotal => _items.Sum(i => i.LineTotal);
    public decimal Vat => Math.Round(_items.Sum(i => i.Product.IsVatApplicable ? i.LineTotal * 0.20m : 0m), 2);
    public decimal Shipping { get; private set; } = 0;
    public decimal Total => Subtotal + Shipping + Vat;

    public bool IsEmpty => _items.Count == 0;

    public event Action? OnChange;

    public bool AddItem(Product product, int qty = 1, string? size = null, string? metal = null)
    {
        if (product.StockLevel <= 0) return false;

        var existing = _items.FirstOrDefault(i => i.Product.Id == product.Id && i.SelectedSize == size && i.SelectedMetal == metal);
        if (existing != null)
        {
            var totalInCart = existing.Quantity + qty;
            existing.Quantity = Math.Min(totalInCart, product.StockLevel);
        }
        else
        {
            _items.Add(new CartItem { Product = product, Quantity = Math.Min(qty, product.StockLevel), SelectedSize = size, SelectedMetal = metal });
        }
        NotifyChange();
        return true;
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
        
        if (qty <= 0) 
            _items.Remove(item);
        else
            item.Quantity = Math.Min(qty, item.Product.StockLevel);
        
        NotifyChange();
    }

    public void SetShipping(decimal cost, bool notify = true) 
    { 
        Shipping = cost; 
        if (notify) NotifyChange(); 
    }

    public void Clear() { _items.Clear(); NotifyChange(); }

    private void NotifyChange()
    {
        if (!_suppressChangeEvent)
            OnChange?.Invoke();
    }

    public IDisposable SuppressChangeEvent()
    {
        _suppressChangeEvent = true;
        return new ChangeEventGuard(this);
    }

    private class ChangeEventGuard : IDisposable
    {
        private readonly CartService _cart;
        public ChangeEventGuard(CartService cart) => _cart = cart;
        public void Dispose() => _cart._suppressChangeEvent = false;
    }
}
