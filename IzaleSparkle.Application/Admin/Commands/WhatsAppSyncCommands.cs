using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;

namespace IzaleSparkle.Application.Admin.Commands;

// ── SYNC PRODUCTS FROM WHATSAPP / META CATALOG ────────────────────────────
/// <summary>
/// Imports products from the linked WhatsApp/Meta Commerce catalog.
/// Matching is by slug (derived from product name): existing products are
/// refreshed (price / description / image), new ones are created.
/// Local-only fields (stock, cost, supplier, category) are preserved on update.
/// </summary>
public record SyncWhatsAppCatalogCommand() : IRequest<WhatsAppSyncResponse>;

public sealed class SyncWhatsAppCatalogCommandHandler(
    IWhatsAppCatalogService catalog, IUnitOfWork uow)
    : IRequestHandler<SyncWhatsAppCatalogCommand, WhatsAppSyncResponse>
{
    public async Task<WhatsAppSyncResponse> Handle(SyncWhatsAppCatalogCommand cmd, CancellationToken ct)
    {
        var items = await catalog.FetchProductsAsync(ct);

        var existing = (await uow.Products.GetAllAsync(ct)).ToList();
        // First product wins if two share a slug — keeps the mapping deterministic.
        var bySlug = existing
            .GroupBy(p => p.Slug)
            .ToDictionary(g => g.Key, g => g.First());

        int created = 0, updated = 0, skipped = 0;
        var messages = new List<string>();

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                skipped++;
                messages.Add($"Skipped a catalog item with no name (retailer id '{item.RetailerId}').");
                continue;
            }

            var slug = item.Name.ToLower().Replace(" ", "-");

            if (bySlug.TryGetValue(slug, out var product))
            {
                // Refresh customer-facing fields only — preserve local stock/admin data.
                if (!string.IsNullOrWhiteSpace(item.Description))
                    Set(product, "Description", item.Description);
                if (item.Price > 0)
                    Set(product, "Price", new Domain.ValueObjects.Money(item.Price));
                if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                    Set(product, "ImageUrl", item.ImageUrl);

                uow.Products.Update(product);
                updated++;
            }
            else
            {
                var product2 = Product.Create(
                    name:        item.Name,
                    description: item.Description,
                    material:    string.Empty,
                    price:       item.Price,
                    category:    "rings",          // default — admin can re-categorise after import
                    imageUrl:    item.ImageUrl);

                if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                    product2.AddImage(item.ImageUrl, isPrimary: true);

                await uow.Products.AddAsync(product2, ct);
                bySlug[slug] = product2;            // guards against duplicates within this batch
                created++;
            }
        }

        await uow.SaveChangesAsync(ct);

        return new WhatsAppSyncResponse(created, updated, skipped, items.Count, messages);
    }

    // Product setters are private — same reflection approach used by the other admin handlers.
    static void Set(object obj, string prop, object? val)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public   |
            System.Reflection.BindingFlags.NonPublic);
        p?.SetValue(obj, val);
    }
}
