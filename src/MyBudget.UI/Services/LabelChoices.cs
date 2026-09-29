using MyBudget.Contracts;

namespace MyBudget.UI.Services;

/// <summary>
/// Which labels to offer next to a category. A label belongs to a category, so listing every one of
/// them turns the field into a wall of unrelated merchants — picking "Fuel" under Healthcare is never
/// what anyone meant.
/// </summary>
public static class LabelChoices
{
    /// <param name="current">
    /// The label already chosen. Always included, even when it does not match the category: a select
    /// whose value is missing from its options falls back to whatever sits at that index, and with
    /// autosave that writes the wrong label.
    /// </param>
    public static IEnumerable<LabelDto> For(IEnumerable<LabelDto>? labels, int? categoryId, int? current = null)
    {
        var active = (labels ?? []).Where(l => l.IsActive || l.Id == current);
        if (categoryId is not { } category) return active.OrderBy(l => l.Name);

        return active
            .Where(l => l.CategoryId == category || l.CategoryId is null || l.Id == current)
            .OrderBy(l => l.CategoryId is null)      // the category's own first, then the unassigned
            .ThenBy(l => l.Name);
    }
}
