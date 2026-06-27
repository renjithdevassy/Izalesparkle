using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Exceptions;

namespace IzaleSparkle.Application.Admin.Commands;

// ── SHARED MAPPER ─────────────────────────────────────────────
internal static class CategoryMapper
{
    internal static CategoryResponse ToResponse(Category c, int productCount = 0) => new(
        c.Id, c.Name, c.Slug, c.Description, c.Icon,
        c.SortOrder, c.IsActive, productCount);
}

// ── GET ALL ───────────────────────────────────────────────────
public record GetCategoriesAdminQuery : IRequest<IEnumerable<CategoryResponse>>;

public sealed class GetCategoriesAdminQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetCategoriesAdminQuery, IEnumerable<CategoryResponse>>
{
    public async Task<IEnumerable<CategoryResponse>> Handle(
        GetCategoriesAdminQuery req, CancellationToken ct)
    {
        var cats     = await uow.Categories.GetAllAsync(ct);
        var products = await uow.Products.GetAllAsync(ct);

        var counts = products
            .GroupBy(p => Category.ToSlug(p.Category))
            .ToDictionary(g => g.Key, g => g.Count());

        return cats
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => CategoryMapper.ToResponse(c,
                counts.GetValueOrDefault(c.Slug, 0)));
    }
}

// ── CREATE ────────────────────────────────────────────────────
public record CreateCategoryAdminCommand(CreateCategoryRequest Request)
    : IRequest<CategoryResponse>;

public sealed class CreateCategoryAdminCommandHandler(IUnitOfWork uow)
    : IRequestHandler<CreateCategoryAdminCommand, CategoryResponse>
{
    public async Task<CategoryResponse> Handle(
        CreateCategoryAdminCommand cmd, CancellationToken ct)
    {
        var req  = cmd.Request;
        var slug = Category.ToSlug(req.Name);

        var existing = await uow.Categories.GetBySlugAsync(slug, ct);
        if (existing != null)
            throw new BusinessRuleException("Slug",
                $"A category with slug '{slug}' already exists.");

        var cat = new Category
        {
            Name        = req.Name.Trim(),
            Slug        = slug,
            Description = req.Description,
            Icon        = string.IsNullOrWhiteSpace(req.Icon) ? "💎" : req.Icon,
            SortOrder   = req.SortOrder,
            IsActive    = req.IsActive,
        };

        await uow.Categories.AddAsync(cat, ct);
        await uow.SaveChangesAsync(ct);
        return CategoryMapper.ToResponse(cat);
    }
}

// ── UPDATE ────────────────────────────────────────────────────
public record UpdateCategoryAdminCommand(UpdateCategoryRequest Request)
    : IRequest<CategoryResponse>;

public sealed class UpdateCategoryAdminCommandHandler(IUnitOfWork uow)
    : IRequestHandler<UpdateCategoryAdminCommand, CategoryResponse>
{
    public async Task<CategoryResponse> Handle(
        UpdateCategoryAdminCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var cat = await uow.Categories.GetByIdAsync(req.Id, ct)
            ?? throw new NotFoundException(nameof(Category), req.Id);
        var oldSlug = cat.Slug;
        var newSlug = Category.ToSlug(req.Name);

        cat.Name        = req.Name.Trim();
        cat.Slug        = newSlug;
        cat.Description = req.Description;
        cat.Icon        = string.IsNullOrWhiteSpace(req.Icon) ? "💎" : req.Icon;
        cat.SortOrder   = req.SortOrder;
        cat.IsActive    = req.IsActive;
        cat.UpdatedAt   = DateTime.UtcNow;

        if (!oldSlug.Equals(newSlug, StringComparison.OrdinalIgnoreCase))
        {
            var products = await uow.Products.GetAllAsync(ct);
            foreach (var product in products.Where(p =>
                         Category.ToSlug(p.Category).Equals(oldSlug, StringComparison.OrdinalIgnoreCase)))
            {
                product.UpdateCategory(newSlug);
                uow.Products.Update(product);
            }
        }

        uow.Categories.Update(cat);
        await uow.SaveChangesAsync(ct);
        return CategoryMapper.ToResponse(cat);
    }
}

// ── DELETE ────────────────────────────────────────────────────
public record DeleteCategoryAdminCommand(int Id) : IRequest<bool>;

public sealed class DeleteCategoryAdminCommandHandler(IUnitOfWork uow)
    : IRequestHandler<DeleteCategoryAdminCommand, bool>
{
    public async Task<bool> Handle(
        DeleteCategoryAdminCommand cmd, CancellationToken ct)
    {
        var cat = await uow.Categories.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Category), cmd.Id);

        uow.Categories.Delete(cat);
        await uow.SaveChangesAsync(ct);
        return true;
    }
}
