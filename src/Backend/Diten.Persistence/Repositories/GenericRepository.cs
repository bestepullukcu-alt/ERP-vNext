using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Diten.Application.Common.Interfaces;
using Diten.Domain.Common;
using Diten.Persistence.Context;
using MongoDB.Driver;

namespace Diten.Persistence.Repositories;

public class GenericRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly MongoDbContext _context;
    protected readonly IMongoCollection<T> _collection;

    public GenericRepository(MongoDbContext context)
    {
        _context = context;
        _collection = _context.GetCollection<T>(typeof(T).Name);
    }

    public async Task<T> GetByIdAsync(string id)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<T>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
    {
        return await _collection.Find(predicate).ToListAsync();
    }

    public async Task<T> AddAsync(T entity)
    {
        await _collection.InsertOneAsync(entity);
        return entity;
    }

    public async Task UpdateAsync(T entity)
    {
        await _collection.ReplaceOneAsync(x => x.Id == entity.Id, entity);
    }

    public async Task DeleteAsync(T entity)
    {
        await _collection.DeleteOneAsync(x => x.Id == entity.Id);
    }
}
