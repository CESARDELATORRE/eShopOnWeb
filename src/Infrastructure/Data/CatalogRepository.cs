using Microsoft.eShopWeb.ApplicationCore.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Data
{
    public class CatalogRepository : ICatalogRepository
    {
        private readonly CatalogContext dbContext;

        public CatalogRepository(CatalogContext dbContext) 
        {
            this.dbContext = dbContext;
        }

        public IEnumerable<string> GetAllProductIds()
        {
            return dbContext.CatalogItems.Select(c => c.Id).ToArray().Select(c => c.ToString());
        }

        public IEnumerable<CatalogItem> GetAllProducts(IEnumerable<int> products, int itemsCount)
        {
            return dbContext.CatalogItems.Where(c => products.Contains(c.Id)).Take(itemsCount);
        }
    }

    public interface ICatalogRepository
    {
        IEnumerable<string> GetAllProductIds();
        IEnumerable<CatalogItem> GetAllProducts(IEnumerable<int> products, int itemsCount);
    }
}
