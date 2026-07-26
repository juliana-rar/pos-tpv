using PosTpv.Application.DTOs;
using PosTpv.Domain.Enums;
using PosTpv.Web.Localization;

namespace PosTpv.Web.Components.Shared;

/// <summary>
/// Grouping/labelling helpers shared by OrderCard, OrderEditor and Kitchen — the three places
/// that render a comanda's lines batched by course and by "served together" / "ordered together".
/// Kept as plain static functions (no Blazor dependency) so the three components can't drift.
/// </summary>
public static class OrderPresentation
{
    public sealed record ServeBatch(DateTime? ServedAt, List<OrderItemDto> Items);
    public sealed record TimeGroup(DateTime Time, List<OrderItemDto> Items);
    public sealed record PartitionedItems(
        List<OrderItemDto> Drinks, List<OrderItemDto> Starters, List<OrderItemDto> Mains, List<OrderItemDto> Desserts);
    public sealed record OrderSection(string? Icon, string? Label, List<OrderItemDto> Items);

    /// <summary>
    /// Below 5 lines an order is short enough to scan as a flat list. From 5 lines on, it's
    /// grouped under the same Drinks/First/Second/Desserts headers as the kitchen and /orders
    /// screens, so a long ticket reads at a glance instead of as one undifferentiated column.
    /// Used by both /pos and the order editor so a long ticket reads the same way in either.
    /// </summary>
    public static List<OrderSection> Sections(IReadOnlyList<OrderItemDto> items)
    {
        if (items.Count < 5)
            return new() { new OrderSection(null, null, items.ToList()) };

        var sections = new List<OrderSection>();
        void AddGroup(string icon, string label, IEnumerable<OrderItemDto> group)
        {
            var list = group.ToList();
            if (list.Count == 0) return;
            sections.Add(new OrderSection(icon, label, list));
        }
        AddGroup("cup-soda", "Drinks", items.Where(i => i.IsDrink));
        AddGroup("salad", "First course", items.Where(i => !i.IsDrink && i.Course == CourseType.Starter));
        AddGroup("utensils", "Second course", items.Where(i => !i.IsDrink && i.Course == CourseType.Main));
        AddGroup("cake-slice", "Desserts", items.Where(i => !i.IsDrink && i.Course == CourseType.Dessert));
        return sections;
    }

    public static string GroupLinesClass(OrderSection section) =>
        section.Items.Count > 6 ? "order__group-lines order__group-lines--scroll" : "order__group-lines";

    /// <summary>Buckets an order's lines into Drinks / First / Second / Desserts, the grouping every serve action row is driven by.</summary>
    public static PartitionedItems Partition(IEnumerable<OrderItemDto> items)
    {
        List<OrderItemDto> drinks = new(), starters = new(), mains = new(), desserts = new();
        foreach (var it in items)
        {
            if (it.IsDrink) { drinks.Add(it); continue; }
            switch (it.Course)
            {
                case CourseType.Starter: starters.Add(it); break;
                case CourseType.Dessert: desserts.Add(it); break;
                default: mains.Add(it); break;   // CourseType.Main
            }
        }
        return new PartitionedItems(drinks, starters, mains, desserts);
    }

    /// <summary>
    /// A "Serve X" click stamps every line it touches with the exact same UpdatedAt (one
    /// SaveChanges = one timestamp), so grouping consecutive lines by that value recovers the
    /// original serve batches: one green badge per batch instead of one per line.
    /// </summary>
    public static List<ServeBatch> GroupByServeBatch(List<OrderItemDto> items)
    {
        var groups = new List<ServeBatch>();
        foreach (var it in items)
        {
            var servedAt = it.Status == OrderItemStatus.Delivered ? it.UpdatedAt : null;
            if (groups.Count > 0 && groups[^1].ServedAt == servedAt)
                groups[^1].Items.Add(it);
            else
                groups.Add(new ServeBatch(servedAt, new List<OrderItemDto> { it }));
        }
        return groups;
    }

    /// <summary>
    /// Groups consecutive lines ordered in the same minute — e.g. three waters tapped one after
    /// another — under one shared timestamp with a connecting bracket, instead of repeating the
    /// same time on every line.
    /// </summary>
    public static List<TimeGroup> GroupByOrderTime(List<OrderItemDto> items)
    {
        var groups = new List<TimeGroup>();
        foreach (var it in items)
        {
            var minute = new DateTime(it.CreatedAt.Year, it.CreatedAt.Month, it.CreatedAt.Day, it.CreatedAt.Hour, it.CreatedAt.Minute, 0, it.CreatedAt.Kind);
            if (groups.Count > 0 && groups[^1].Time == minute)
                groups[^1].Items.Add(it);
            else
                groups.Add(new TimeGroup(minute, new List<OrderItemDto> { it }));
        }
        return groups;
    }

    public static string DrinksLabel(bool hasPendingDrinks, DateTime? drinksServedAt) => hasPendingDrinks
        ? Loc.T("Serve drinks")
        : drinksServedAt is null
            ? Loc.T("Drinks served")
            : $"{Loc.T("Drinks served")} · {drinksServedAt.Value.ToLocalTime():HH:mm}";

    /// <summary>Label + last-served timestamp for a course's serve/undo button, e.g. "Firsts served · 14:32".</summary>
    public static string CourseServeLabel(bool pendingToServe, List<OrderItemDto> items, string serveKey, string servedKey)
    {
        if (pendingToServe) return Loc.T(serveKey);
        var lastServedAt = items.Where(s => s.Status == OrderItemStatus.Delivered).Max(s => (DateTime?)s.UpdatedAt);
        return lastServedAt is null ? Loc.T(servedKey) : $"{Loc.T(servedKey)} · {lastServedAt.Value.ToLocalTime():HH:mm}";
    }

    /// <summary>
    /// Once seconds have been fired, the button carries both timestamps at once — when the
    /// kitchen prepared them and, once served, when the waiter served them — rather than losing
    /// the "prepared" time the moment it flips to "Serve"/"Served".
    /// </summary>
    public static string SecondsServeLabel(bool pendingToServe, List<OrderItemDto> mains, DateTime? secondsSentAt)
    {
        var prepared = secondsSentAt is { } sentAt ? $"{Loc.T("Seconds prepared")} · {sentAt.ToLocalTime():HH:mm}" : null;
        var serveOrServed = CourseServeLabel(pendingToServe, mains, "Serve seconds", "Seconds served");
        return prepared is null ? serveOrServed : $"{prepared} · {serveOrServed}";
    }
}
