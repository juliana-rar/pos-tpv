using PosTpv.Application.Common.Interfaces;
using PosTpv.Domain.Common;

namespace PosTpv.Application.Common;

/// <summary>Shared "swap with neighbour" reordering used by products, categories and category comments.</summary>
public static class ReorderHelper
{
    /// <summary>
    /// Swaps <paramref name="id"/>'s DisplayOrder with its neighbour in <paramref name="direction"/>
    /// (-1 = up, +1 = down) within the already-loaded, already-ordered <paramref name="ordered"/> list.
    /// Reindexes the whole list first so the swap still moves the row when DisplayOrder values collide
    /// (e.g. everything at 0). Returns false (no-op, caller should skip SaveChangesAsync) when the id
    /// isn't found or the move would go out of bounds.
    /// </summary>
    public static bool TrySwap<T>(IRepository<T> repo, List<T> ordered, int id, int direction)
        where T : BaseEntity, IOrderable
    {
        var index = ordered.FindIndex(x => x.Id == id);
        var target = index + Math.Sign(direction);
        if (index < 0 || target < 0 || target >= ordered.Count) return false;

        for (var i = 0; i < ordered.Count; i++) ordered[i].DisplayOrder = i;
        (ordered[index].DisplayOrder, ordered[target].DisplayOrder) = (target, index);
        repo.Update(ordered[index]);
        repo.Update(ordered[target]);
        return true;
    }
}
