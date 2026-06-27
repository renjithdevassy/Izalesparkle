using Microsoft.JSInterop;

namespace IzaleSparkle.Client.Services;

public class WishlistService
{
    private const string WishlistKey = "izale_wishlist";
    private readonly HashSet<int> _items = new();
    private IJSRuntime? _js;

    public event Action? OnChange;

    public IReadOnlyCollection<int> Items => _items;
    public int Count => _items.Count;

    public async Task InitialiseAsync(IJSRuntime js)
    {
        _js = js;
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", WishlistKey);
            if (!string.IsNullOrEmpty(stored))
            {
                var ids = stored.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => { int.TryParse(s, out var id); return id; })
                    .Where(id => id > 0)
                    .ToHashSet();
                _items.UnionWith(ids);
            }
        }
        catch { }
    }

    public bool IsWishlisted(int productId) => _items.Contains(productId);

    public async Task ToggleAsync(int productId)
    {
        if (_items.Contains(productId))
            _items.Remove(productId);
        else
            _items.Add(productId);

        await SaveAsync();
        OnChange?.Invoke();
    }

    public async Task AddAsync(int productId)
    {
        if (_items.Add(productId))
        {
            await SaveAsync();
            OnChange?.Invoke();
        }
    }

    public async Task RemoveAsync(int productId)
    {
        if (_items.Remove(productId))
        {
            await SaveAsync();
            OnChange?.Invoke();
        }
    }

    public async Task ClearAsync()
    {
        _items.Clear();
        await SaveAsync();
        OnChange?.Invoke();
    }

    private async Task SaveAsync()
    {
        if (_js == null) return;
        try
        {
            var value = string.Join(",", _items);
            await _js.InvokeVoidAsync("localStorage.setItem", WishlistKey, value);
        }
        catch { }
    }
}
