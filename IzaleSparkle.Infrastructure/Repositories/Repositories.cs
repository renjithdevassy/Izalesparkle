using Microsoft.EntityFrameworkCore;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Infrastructure.Persistence;

namespace IzaleSparkle.Infrastructure.Repositories;

// ── GENERIC REPOSITORY ────────────────────────────────────────
public class Repository<T>(AppDbContext db) : IRepository<T> where T : class
{
    protected readonly AppDbContext _db = db;
    protected readonly DbSet<T> _set = db.Set<T>();

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _set.FindAsync([id], ct);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await _set.ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);
    public void Delete(T entity) => _set.Remove(entity);
}

// ── PRODUCT REPOSITORY ────────────────────────────────────────
public class ProductRepository(AppDbContext db)
    : Repository<Product>(db), IProductRepository
{
    public async Task<IEnumerable<Product>> GetByCategoryAsync(string? category, CancellationToken ct = default)
    {
        var q = _db.Products.Include(p => p.Images).Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(category) && category != "all")
            q = q.Where(p => p.Category.ToString().ToLower() == category.ToLower());
        return await q.ToListAsync(ct);
    }

    public async Task<IEnumerable<Product>> SearchAsync(
        string? query, string? category, decimal? minPrice, decimal? maxPrice, CancellationToken ct = default)
    {
        var q = _db.Products.Include(p => p.Images).Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(category) && category != "all")
            q = q.Where(p => p.Category.ToString().ToLower() == category.ToLower());

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Name.Contains(query) || p.Description.Contains(query));

        if (minPrice.HasValue) q = q.Where(p => p.Price.Amount >= minPrice);
        if (maxPrice.HasValue) q = q.Where(p => p.Price.Amount <= maxPrice);

        return await q.ToListAsync(ct);
    }

    public async Task<IEnumerable<Product>> GetFeaturedAsync(int count, CancellationToken ct = default)
        => await _db.Products.Include(p => p.Images)
               .Where(p => p.IsActive)
               .OrderBy(p => p.Id)
               .Take(count)
               .ToListAsync(ct);

    public async Task<IEnumerable<Product>> GetRelatedAsync(int productId, int count, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync([productId], ct);
        if (product == null) return [];

        return await _db.Products
            .Include(p => p.Images)
            .Where(p => p.IsActive && p.Id != productId && p.Category == product.Category)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalCountAsync(string? category, string? query, CancellationToken ct = default)
    {
        var q = _db.Products.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(category) && category != "all")
            q = q.Where(p => p.Category.ToString().ToLower() == category.ToLower());
        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Name.Contains(query));
        return await q.CountAsync(ct);
    }

    // Override GetAllAsync to include images (admin panel needs them all)
    public new async Task<IEnumerable<Product>> GetAllAsync(CancellationToken ct = default)
        => await _db.Products
               .Include(p => p.Images)
               .OrderByDescending(p => p.CreatedAt)
               .ToListAsync(ct);

    public override async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Products
               .Include(p => p.Images)
               .Include(p => p.Reviews)
               .FirstOrDefaultAsync(p => p.Id == id, ct);
}

// ── ORDER REPOSITORY ──────────────────────────────────────────
public class OrderRepository(AppDbContext db)
    : Repository<Order>(db), IOrderRepository
{
    public new async Task<IEnumerable<Order>> GetAllAsync(CancellationToken ct = default)
        => await _db.Orders
               .Include(o => o.Items)
               .OrderByDescending(o => o.CreatedAt)
               .ToListAsync(ct);

    public new async Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Orders
               .Include(o => o.Items)
               .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
        => await _db.Orders
               .Include(o => o.Items)
               .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

    public async Task<IEnumerable<Order>> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _db.Orders
               .Include(o => o.Items)
               .Where(o => o.CustomerEmail == email)
               .OrderByDescending(o => o.CreatedAt)
               .ToListAsync(ct);
}

// ── REVIEW REPOSITORY ─────────────────────────────────────────
public class ReviewRepository(AppDbContext db)
    : Repository<Review>(db), IReviewRepository
{
    public async Task<IEnumerable<Review>> GetByProductIdAsync(int productId, CancellationToken ct = default)
        => await _db.Reviews
               .Where(r => r.ProductId == productId)
               .OrderByDescending(r => r.HelpfulCount)
               .ToListAsync(ct);
}


// ── USER REPOSITORY ───────────────────────────────────────────
public class UserRepository(AppDbContext db)
    : Repository<AppUser>(db), IUserRepository
{
    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim(), ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await _db.Users.AnyAsync(u => u.Email == email.ToLower().Trim(), ct);
}

// ── CATEGORY REPOSITORY ───────────────────────────────────────
public class CategoryRepository(AppDbContext db)
    : Repository<Category>(db), ICategoryRepository
{
    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public new async Task<IEnumerable<Category>> GetAllActiveAsync(CancellationToken ct = default)
        => await _db.Categories
               .Where(c => c.IsActive)
               .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
               .ToListAsync(ct);
}

// ── DISCOUNT CODE REPOSITORY ──────────────────────────────────
public class DiscountCodeRepository(AppDbContext db)
    : Repository<DiscountCode>(db), IDiscountCodeRepository
{
    public async Task<DiscountCode?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _db.DiscountCodes
               .FirstOrDefaultAsync(d => d.Code.ToLower() == code.ToLower(), ct);

    public new async Task<IEnumerable<DiscountCode>> GetAllAsync(CancellationToken ct = default)
        => await _db.DiscountCodes.OrderBy(d => d.Code).ToListAsync(ct);
}

// ── UNIT OF WORK ─────────────────────────────────────────────
public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private IProductRepository? _products;
    private ICategoryRepository?     _categories;
    private IDiscountCodeRepository? _discountCodes;
    private IOrderRepository?   _orders;
    private IReviewRepository?  _reviews;
    private IUserRepository?    _users;

    public IProductRepository Products => _products ??= new ProductRepository(db);
    public ICategoryRepository     Categories    => _categories    ??= new CategoryRepository(db);
    public IDiscountCodeRepository DiscountCodes => _discountCodes ??= new DiscountCodeRepository(db);
    public IOrderRepository   Orders   => _orders   ??= new OrderRepository(db);
    public IReviewRepository  Reviews  => _reviews  ??= new ReviewRepository(db);
    public IUserRepository    Users    => _users    ??= new UserRepository(db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async ValueTask DisposeAsync() => await db.DisposeAsync();
}
