using System.Linq.Expressions;
using Contracts.Domain.Database;
using Marten;
using Marten.Linq;

namespace App.Api.Accessors
{
    /// <summary>
    /// Provides access methods to perform CRUD and query operations on Marten document database entities.
    /// Supports generic storage, retrieval, and query of any entity extending BaseEntity.
    /// </summary>
    public class DatabaseAccessor : IDatabaseAccessor
    {
        private readonly IDocumentStore _store;

        /// <summary>
        /// Constructs the DatabaseAccessor with an injected Marten IDocumentStore.
        /// </summary>
        public DatabaseAccessor(IDocumentStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// Inserts or updates a document of type T in the database.
        /// </summary>
        public async Task UpsertDocument<T>(T document) where T : BaseEntity
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            await using IDocumentSession session = _store.LightweightSession();
            session.Store(document);
            await session.SaveChangesAsync();
        }

        /// <summary>
        /// Inserts a new document of type T with a new Guid Id, ensuring it's treated as new.
        /// </summary>
        public async Task InsertDocument<T>(T document) where T : BaseEntity
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            document.Id = Guid.NewGuid(); // Ensure new document
            await using IDocumentSession session = _store.LightweightSession();
            session.Insert(document);
            await session.SaveChangesAsync();
        }

        /// <summary>
        /// Deletes a document from the database.
        /// </summary>
        public async Task DeleteDocument<T>(T document) where T : BaseEntity
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            await using IDocumentSession session = _store.LightweightSession();
            session.Delete(document);
            await session.SaveChangesAsync();
        }

        /// <summary>
        /// Retrieves all documents of the specified type, optionally skipping and taking for paging.
        /// </summary>
        public async Task<IEnumerable<T>> GetAllDocuments<T>(int skip = 0, int take = int.MaxValue) where T : BaseEntity
        {
            await using IQuerySession session = _store.QuerySession();
            return await session.Query<T>().Skip(skip).Take(take).ToListAsync();
        }

        /// <summary>
        /// Loads a single document by its Guid Id.
        /// </summary>
        public async Task<T?> GetDocumentById<T>(Guid id) where T : BaseEntity
        {
            await using IQuerySession session = _store.QuerySession();
            return await session.LoadAsync<T>(id);
        }

        /// <summary>
        /// Loads documents by a list of Ids. Supports paging by skip and take parameters.
        /// </summary>
        public async Task<IEnumerable<T>> GetDocumentsByIds<T>(IList<Guid> ids, int skip = 0, int take = int.MaxValue) where T : BaseEntity
        {
            if (ids == null || !ids.Any()) throw new ArgumentNullException(nameof(ids));

            using IQuerySession session = _store.QuerySession();
            return await session.Query<T>().Where(x => ids.Contains(x.Id)).Skip(skip).Take(take).ToListAsync();
        }

        /// <summary>
        /// Updates an existing document.
        /// </summary>
        public async Task UpdateDocument<T>(T document) where T : BaseEntity
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            await using IDocumentSession session = _store.LightweightSession();
            session.Update(document);
            await session.SaveChangesAsync();
        }

        /// <summary>
        /// Retrieves the first document matching the provided predicate.
        /// </summary>
        public async Task<T?> GetDocumentByProperty<T>(Expression<Func<T, bool>> predicate) where T : BaseEntity
        {
            await using IQuerySession session = _store.QuerySession();
            return await session.Query<T>().FirstOrDefaultAsync(predicate);
        }

        /// <summary>
        /// Retrieves documents matching a predicate, with paging and optional ordering.
        /// </summary>
        public async Task<IEnumerable<T>> GetDocumentsByProperty<T>(
            Expression<Func<T, bool>> predicate,
            int? skip = null,
            int? take = null,
            Expression<Func<T, object>>? orderby = null,
            bool descending = true) where T : BaseEntity
        {
            await using IQuerySession session = _store.QuerySession();
            var query = session.Query<T>().Where(predicate);

            if (orderby != null)
            {
                query = descending
                    ? query.OrderByDescending(orderby)
                    : query.OrderBy(orderby);
            }

            if (skip.HasValue)
                query = query.Skip(skip.Value);

            if (take.HasValue)
                query = query.Take(take.Value);

            return await query.ToListAsync();
        }

        /// <summary>
        /// Retrieves documents matching a set of predicates, with paging and optional ordering.
        /// </summary>
        public async Task<IEnumerable<T>> GetDocumentsByProperty<T>(
            IEnumerable<Expression<Func<T, bool>>> predicates,
            int? skip = null,
            int? take = null,
            Expression<Func<T, object>>? orderBy = null,
            bool descending = true) where T : BaseEntity
        {
            using IQuerySession session = _store.QuerySession();
            var query = session.Query<T>();

            foreach (var predicate in predicates)
            {
                query = (IMartenQueryable<T>)query.Where(predicate);
            }

            if (orderBy != null)
            {
                query = descending ? (IMartenQueryable<T>)query.OrderByDescending(orderBy) : (IMartenQueryable<T>)query.OrderBy(orderBy);
            }

            if (skip.HasValue)
            {
                query = (IMartenQueryable<T>)query.Skip(skip.Value);
            }

            if (take.HasValue)
            {
                query = (IMartenQueryable<T>)query.Take(take.Value);
            }

            return await query.ToListAsync();
        }
    }
}
