using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Contracts.Domain.Database;
using App.Api;
using App.Api.Utilities;

namespace App.Api.Accessors
{
    public class LocalDatabaseAccessor : IDatabaseAccessor
    {
        private readonly string _rootDirectory;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public LocalDatabaseAccessor(AppConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _rootDirectory = LocalPathHelper.GetAppResourcePath(config.AppName);
            Directory.CreateDirectory(_rootDirectory);
        }

        private string GetFilePath<T>()
        {
            return Path.Combine(_rootDirectory, typeof(T).Name + ".json");
        }

        private async Task<List<T>> LoadAll<T>() where T : BaseEntity
        {
            var path = GetFilePath<T>();
            if (!File.Exists(path))
                return new List<T>();
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions) ?? new List<T>();
        }

        private async Task SaveAll<T>(IEnumerable<T> items) where T : BaseEntity
        {
            var path = GetFilePath<T>();
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, items.ToList(), JsonOptions);
        }

        public async Task UpsertDocument<T>(T document) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            var idx = list.FindIndex(x => x.Id == document.Id);
            if (idx >= 0)
                list[idx] = document;
            else
                list.Add(document);
            await SaveAll(list);
        }

        public async Task InsertDocument<T>(T document) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            document.Id = Guid.NewGuid();
            list.Add(document);
            await SaveAll(list);
        }

        public async Task DeleteDocument<T>(T document) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            list = list.Where(x => x.Id != document.Id).ToList();
            await SaveAll(list);
        }

        public async Task<IEnumerable<T>> GetAllDocuments<T>(int skip = 0, int take = int.MaxValue) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            return list.Skip(skip).Take(take).ToList();
        }

        public async Task<T?> GetDocumentById<T>(Guid id) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            return list.FirstOrDefault(x => x.Id == id);
        }

        public async Task<IEnumerable<T>> GetDocumentsByIds<T>(IList<Guid> ids, int skip = 0, int take = int.MaxValue) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            return list.Where(x => ids.Contains(x.Id)).Skip(skip).Take(take).ToList();
        }

        public async Task UpdateDocument<T>(T document) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            var idx = list.FindIndex(x => x.Id == document.Id);
            if (idx >= 0)
                list[idx] = document;
            else
                throw new InvalidOperationException($"Cannot update {typeof(T).Name}: not found.");
            await SaveAll(list);
        }

        public async Task<T?> GetDocumentByProperty<T>(Expression<Func<T, bool>> predicate) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            return list.AsQueryable().FirstOrDefault(predicate);
        }

        public async Task<IEnumerable<T>> GetDocumentsByProperty<T>(Expression<Func<T, bool>> predicate,
            int? skip = null, int? take = null,
            Expression<Func<T, object>>? orderby = null, bool descending = true) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            var query = list.AsQueryable().Where(predicate);
            if (orderby != null)
                query = descending ? query.OrderByDescending(orderby) : query.OrderBy(orderby);
            if (skip.HasValue)
                query = query.Skip(skip.Value);
            if (take.HasValue)
                query = query.Take(take.Value);
            return query.ToList();
        }

        public async Task<IEnumerable<T>> GetDocumentsByProperty<T>(IEnumerable<Expression<Func<T, bool>>> predicates,
            int? skip = null, int? take = null,
            Expression<Func<T, object>>? orderBy = null, bool descending = true) where T : BaseEntity
        {
            var list = await LoadAll<T>();
            var query = list.AsQueryable();
            foreach (var predicate in predicates)
                query = query.Where(predicate);
            if (orderBy != null)
                query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
            if (skip.HasValue)
                query = query.Skip(skip.Value);
            if (take.HasValue)
                query = query.Take(take.Value);
            return query.ToList();
        }
    }
}
