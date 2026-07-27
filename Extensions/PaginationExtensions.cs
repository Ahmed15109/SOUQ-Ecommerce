using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.ViewModels;

namespace EcommerceApp.Extensions
{
    public static class PaginationExtensions
    {
        public static async Task<PagedResult<T>> ToPagedListAsync<T>(
            this IQueryable<T> query,
            int pageNumber,
            int pageSize,
            int defaultPageSize = 20,
            int maxPageSize = 100)
        {
            int validPageSize = pageSize <= 0 ? defaultPageSize : Math.Min(pageSize, maxPageSize);

            int totalCount = query is IAsyncEnumerable<T>
                ? await query.CountAsync()
                : query.Count();

            int totalPages = validPageSize > 0 ? (int)Math.Ceiling((double)totalCount / validPageSize) : 0;

            // 3. Validate & clamp page number
            int validPageNumber = totalPages == 0
                ? 1
                : Math.Clamp(pageNumber, 1, totalPages);

            int skip = checked((validPageNumber - 1) * validPageSize);

            var items = totalCount > 0
                ? (query is IAsyncEnumerable<T>
                    ? await query.Skip(skip).Take(validPageSize).ToListAsync()
                    : query.Skip(skip).Take(validPageSize).ToList())
                : new List<T>();

            return new PagedResult<T>(items, totalCount, validPageNumber, validPageSize);
        }
    }
}
