using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-3C (K-7, S-4) — the rep's per-doctor product pick on the planning session, written through the EXISTING
/// selection create / update (no new command). One place for its shape and its checks:
/// <list type="bullet">
/// <item><b>Merge (D9)</b> — a doctor whose <c>Products</c> is null keeps the stored pick (matched by contact + account,
/// else by contact); <c>[]</c> clears it; a list replaces it. Duplicates of one product keep the first.</item>
/// <item><b>Checks</b> — at most <see cref="PlanningSessionProductLimits.MaxPerDoctor"/> per doctor (400
/// <c>too_many_products</c>); role <c>promo</c> / <c>non-promo</c> or empty (400 <c>invalid_product_role</c>); the product
/// exists in MDM (400 <c>product_not_found</c>) — proven through the shared fail-closed MDM Global Product seam
/// (<see cref="IStrategyTemplateProductReferenceValidator"/>): MDM unreachable ⇒ 503 <c>product_lookup_unavailable</c>,
/// nothing is written. A product already stored on the session is not asked again.</item>
/// </list>
/// </summary>
public static class PlanningSessionProductPick
{
    public const string TooManyProducts = "too_many_products";
    public const string InvalidProductRole = "invalid_product_role";
    public const string ProductNotFound = "product_not_found";
    public const string ProductLookupUnavailable = "product_lookup_unavailable";

    /// <summary>The stored form of one doctor's requested pick (null when the request leaves it unchanged).</summary>
    public static List<PlanningSessionSelectedProduct>? Normalize(IReadOnlyList<SelectedProductInput>? products)
        => products?
            .Where(p => p.ProductId != Guid.Empty)
            .GroupBy(p => p.ProductId)
            .Select(g => g.First())
            .Select(p => new PlanningSessionSelectedProduct
            {
                ProductId = p.ProductId,
                ProductCode = string.IsNullOrWhiteSpace(p.ProductCode) ? null : p.ProductCode.Trim(),
                Role = NormalizeRole(p.Role)
            })
            .ToList();

    /// <summary>promo / non-promo, or null when not given (read as promo, K-7d).</summary>
    public static string? NormalizeRole(string? role)
        => string.IsNullOrWhiteSpace(role) ? null : role.Trim().ToLowerInvariant();

    /// <summary>The products of <paramref name="contacts"/>, a null pick taking the stored one (D9).</summary>
    public static List<PlanningSessionSelectedProduct> Merge(
        SelectedContactInput input, IReadOnlyList<PlanningSessionSelectedContact> stored)
    {
        var requested = Normalize(input.Products);
        if (requested is not null)
        {
            return requested;
        }

        var match = stored.FirstOrDefault(s => s.ContactId == input.ContactId && s.AccountId == input.AccountId)
                    ?? stored.FirstOrDefault(s => s.ContactId == input.ContactId);
        return match?.Products.Select(p => new PlanningSessionSelectedProduct
        {
            ProductId = p.ProductId, ProductCode = p.ProductCode, Role = p.Role
        }).ToList() ?? new List<PlanningSessionSelectedProduct>();
    }

    /// <summary>Checks every pick the request SETS; null when all pass, else the refusal (400 / 503).</summary>
    public static async Task<Response<T>?> ValidateAsync<T>(
        IReadOnlyList<SelectedContactInput>? contacts,
        IReadOnlyList<PlanningSessionSelectedContact> stored,
        IStrategyTemplateProductReferenceValidator? products,
        CancellationToken cancellationToken)
    {
        var picks = (contacts ?? Array.Empty<SelectedContactInput>())
            .Select(c => Normalize(c.Products))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
        if (picks.Count == 0)
        {
            return null;
        }

        if (picks.Any(p => p.Count > PlanningSessionProductLimits.MaxPerDoctor))
        {
            return Response<T>.Fail(
                new[] { TooManyProducts, $"At most {PlanningSessionProductLimits.MaxPerDoctor} products per doctor." }, 400);
        }

        if (picks.SelectMany(p => p).Any(p => p.Role is not null
                && p.Role is not (StrategyProductLineRoles.Promo or StrategyProductLineRoles.NonPromo)))
        {
            return Response<T>.Fail(new[] { InvalidProductRole, "A product role must be 'promo' or 'non-promo'." }, 400);
        }

        if ((contacts ?? Array.Empty<SelectedContactInput>())
            .Any(c => c.Products is not null && c.Products.Any(p => p.ProductId == Guid.Empty)))
        {
            return Response<T>.Fail(new[] { ProductNotFound, "A product id is required." }, 400);
        }

        var known = stored.SelectMany(s => s.Products).Select(p => p.ProductId).ToHashSet();
        var toCheck = picks.SelectMany(p => p).Select(p => p.ProductId).Where(id => !known.Contains(id)).Distinct().ToList();
        if (toCheck.Count == 0)
        {
            return null;
        }

        if (products is null)
        {
            return Response<T>.Fail(new[] { ProductLookupUnavailable, "The product master cannot be reached." }, 503);
        }

        foreach (var id in toCheck)
        {
            var outcome = await products.ValidateAsync(
                IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct, id, cancellationToken);
            if (outcome == IStrategyTemplateProductReferenceValidator.Outcome.Unavailable)
            {
                return Response<T>.Fail(new[] { ProductLookupUnavailable, "The product master cannot be reached." }, 503);
            }

            if (outcome != IStrategyTemplateProductReferenceValidator.Outcome.Valid)
            {
                return Response<T>.Fail(new[] { ProductNotFound, $"Product {id} was not found." }, 400);
            }
        }

        return null;
    }
}
