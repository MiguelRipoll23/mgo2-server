using Mgo2Server.Shared.Errors;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.News;

/// <summary>Owns the news articles delivered to the gate and the lobby bulletin.</summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class NewsService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Lists every article, newest first.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<NewsArticle>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.NewsArticles
            .AsNoTracking()
            .OrderByDescending(article => article.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Finds one article.</summary>
    /// <param name="identifier">Identifier of the article.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="ServerException">Thrown when the article does not exist.</exception>
    public async Task<NewsArticle> FindByIdAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.NewsArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(article => article.Identifier == identifier, cancellationToken)
            ?? throw new ServerException("NOT_FOUND", "News item not found", 404);
    }

    /// <summary>Creates an article.</summary>
    /// <param name="article">Article to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<NewsArticle> CreateAsync(NewsArticle article, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        context.NewsArticles.Add(article);
        await context.SaveChangesAsync(cancellationToken);
        return article;
    }

    /// <summary>Updates an article.</summary>
    /// <param name="identifier">Identifier of the article.</param>
    /// <param name="update">Changes to apply.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="ServerException">Thrown when the article does not exist.</exception>
    public async Task<NewsArticle> UpdateAsync(
        int identifier,
        Action<NewsArticle> update,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var article = await context.NewsArticles
            .FirstOrDefaultAsync(item => item.Identifier == identifier, cancellationToken)
            ?? throw new ServerException("NOT_FOUND", "News item not found", 404);

        update(article);
        await context.SaveChangesAsync(cancellationToken);
        return article;
    }

    /// <summary>Deletes an article.</summary>
    /// <param name="identifier">Identifier of the article.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="ServerException">Thrown when the article does not exist.</exception>
    public async Task RemoveAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var removed = await context.NewsArticles
            .Where(article => article.Identifier == identifier)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed == 0)
        {
            throw new ServerException("NOT_FOUND", "News item not found", 404);
        }
    }
}
