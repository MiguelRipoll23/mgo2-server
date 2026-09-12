namespace Mgo2Server.Shared.Utils;

/// <summary>Splits a list into pages of at most a fixed size.</summary>
public static class PayloadHelper
{
    /// <summary>
    /// Splits <paramref name="elements"/> into pages of at most
    /// <paramref name="maximumPerPage"/> entries and hands each page to
    /// <paramref name="consumer"/>. An empty list produces no page at all.
    /// </summary>
    /// <typeparam name="TElement">Type of the elements.</typeparam>
    /// <param name="elements">Elements to page.</param>
    /// <param name="maximumPerPage">Maximum number of elements per page.</param>
    /// <param name="consumer">Receives each page in order.</param>
    public static void ForEachPage<TElement>(
        IReadOnlyList<TElement> elements,
        int maximumPerPage,
        Action<List<TElement>> consumer)
    {
        for (var start = 0; start < elements.Count; start += maximumPerPage)
        {
            var count = Math.Min(maximumPerPage, elements.Count - start);
            var page = new List<TElement>(count);
            for (var index = 0; index < count; index++)
            {
                page.Add(elements[start + index]);
            }

            consumer(page);
        }
    }
}
