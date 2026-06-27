using AutoMapper;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;

namespace IzaleSparkle.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ── Product → ProductResponse ─────────────────────────────────────────
        // ConstructUsing builds the positional record via its constructor.
        // ForAllMembers(Ignore) stops AutoMapper trying to set init-only properties
        // after construction (which fails on positional records).
        CreateMap<Product, ProductResponse>()
            .ConstructUsing(s => new ProductResponse(
                Id:          s.Id,
                Name:        s.Name,
                Slug:        s.Slug,
                Description: s.Description,
                Material:    s.Material,
                Price:       s.Price.Amount,
                OldPrice:    s.OldPrice == null ? null : s.OldPrice.Amount,
                Category:    Category.ToSlug(s.Category),
                Badge:       s.Badge == null ? null : s.Badge.ToString()!.ToLower(),
                Stars:       s.Stars,
                ImageUrl:    s.ImageUrl,
                Images:      s.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
                IsOnSale:       s.IsOnSale,
                SavePercent:    s.SavePercent,
                StockLevel:     s.StockLevel,
                IsVatApplicable:s.IsVatApplicable
            ))
            .ForAllMembers(o => o.Ignore());

        // ── Product → ProductSummaryResponse ─────────────────────────────────
        CreateMap<Product, ProductSummaryResponse>()
            .ConstructUsing(s => new ProductSummaryResponse(
                Id:          s.Id,
                Name:        s.Name,
                Slug:        s.Slug,
                Material:    s.Material,
                Price:       s.Price.Amount,
                OldPrice:    s.OldPrice == null ? null : s.OldPrice.Amount,
                Category:    Category.ToSlug(s.Category),
                Badge:       s.Badge == null ? null : s.Badge.ToString()!.ToLower(),
                Stars:       s.Stars,
                ImageUrl:    s.ImageUrl,
                IsOnSale:        s.IsOnSale,
                SavePercent:     s.SavePercent,
                IsVatApplicable: s.IsVatApplicable,
                StockLevel:      s.StockLevel
            ))
            .ForAllMembers(o => o.Ignore());

        // ── Review → ReviewResponse ───────────────────────────────────────────
        CreateMap<Review, ReviewResponse>()
            .ConstructUsing(s => new ReviewResponse(
                Id:           s.Id,
                AuthorName:   s.AuthorName,
                Date:         s.CreatedAt.ToString("d MMMM yyyy"),
                Stars:        s.Stars,
                Title:        s.Title,
                Body:         s.Body,
                Verified:     s.Verified,
                HelpfulCount: s.HelpfulCount
            ))
            .ForAllMembers(o => o.Ignore());

        // ── Order → OrderResponse ─────────────────────────────────────────────
        // Uses the (source, context) overload so Items can be recursively mapped.
        CreateMap<Order, OrderResponse>()
            .ConstructUsing((s, ctx) => new OrderResponse(
                Id:            s.Id,
                OrderNumber:   s.OrderNumber,
                CustomerEmail: s.CustomerEmail,
                Status:        s.Status.ToString(),
                Subtotal:      s.Subtotal.Amount,
                Shipping:      s.ShippingCost.Amount,
                Vat:           s.Vat.Amount,
                Discount:      s.Discount.Amount,
                Total:         s.Total.Amount,
                PromoCode:     s.PromoCode,
                CreatedAt:     s.CreatedAt,
                Items:         ctx.Mapper.Map<List<OrderItemResponse>>(s.Items)
            ))
            .ForAllMembers(o => o.Ignore());

        // ── OrderItem → OrderItemResponse ─────────────────────────────────────
        CreateMap<OrderItem, OrderItemResponse>()
            .ConstructUsing(s => new OrderItemResponse(
                ProductId:   s.ProductId,
                ProductName: s.ProductName,
                ImageUrl:    s.ImageUrl,
                UnitPrice:   s.UnitPrice.Amount,
                Quantity:    s.Quantity,
                LineTotal:   s.LineTotal.Amount,
                Metal:       FormatMetal(s.Metal),
                Size:        s.Size
            ))
            .ForAllMembers(o => o.Ignore());
    }

    private static string FormatMetal(IzaleSparkle.Domain.Enums.MetalType metal) => metal switch
    {
        IzaleSparkle.Domain.Enums.MetalType.WhiteGold18K  => "18K White Gold",
        IzaleSparkle.Domain.Enums.MetalType.YellowGold18K => "18K Yellow Gold",
        IzaleSparkle.Domain.Enums.MetalType.RoseGold18K   => "18K Rose Gold",
        IzaleSparkle.Domain.Enums.MetalType.Platinum      => "Platinum",
        _                                                  => metal.ToString()
    };
}
