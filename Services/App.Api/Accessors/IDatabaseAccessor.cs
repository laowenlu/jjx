using System.Linq.Expressions;
using Contracts.Domain.Database;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace App.Api.Accessors
{
    public interface IDatabaseAccessor
    {
        Task UpsertDocument<T>(T document) where T : BaseEntity;
        Task InsertDocument<T>(T document) where T : BaseEntity;
        Task DeleteDocument<T>(T document) where T : BaseEntity;
        Task<IEnumerable<T>> GetAllDocuments<T>(int skip = 0, int take = int.MaxValue) where T : BaseEntity;
        Task<T?> GetDocumentById<T>(Guid id) where T : BaseEntity;
        Task<IEnumerable<T>> GetDocumentsByIds<T>(IList<Guid> ids, int skip = 0, int take = int.MaxValue) where T : BaseEntity;
        Task UpdateDocument<T>(T document) where T : BaseEntity;
        Task<T?> GetDocumentByProperty<T>(Expression<Func<T, bool>> predicate) where T : BaseEntity;
        Task<IEnumerable<T>> GetDocumentsByProperty<T>(Expression<Func<T, bool>> predicate,
            int? skip = null, int? take = null,
            Expression<Func<T, object>>? orderby = null, bool descending = true) where T : BaseEntity;
        Task<IEnumerable<T>> GetDocumentsByProperty<T>(IEnumerable<Expression<Func<T, bool>>> predicates,
            int? skip = null, int? take = null,
            Expression<Func<T, object>>? orderBy = null, bool descending = true) where T : BaseEntity;
    }
}