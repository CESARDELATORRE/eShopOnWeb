using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Entities
{
    public class CatalogItem : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PictureUri { get; set; }
        public int CatalogTypeId { get; set; }
        public CatalogType CatalogType { get; set; }
        public int CatalogBrandId { get; set; }
        public CatalogBrand CatalogBrand { get; set; }

        // Id,CatalogBrandId,CatalogTypeId,Description,Name,PictureFileName,Price,AvailableStock,MaxStockThreshold,OnReorder,RestockThreshold
        // 100,100,100,INFLATABLE POLITICAL GLOBE,INFLATABLE POLITICAL GLOBE,100.jpg,0.8500000000000006,4,5,0,1

        public static IEnumerable<CatalogItem> ReadFromCsv(string file)
        {
            return File.ReadAllLines(file)
                .Skip(1) // skip header
                .Select(x => x.Split(','))
                .Select(x => new CatalogItem()
                {
                    Id = int.Parse(x[0]),
                    CatalogBrandId = int.Parse(x[1]),
                    CatalogTypeId = int.Parse(x[2]),
                    Description = x[3],
                    Name = x[4],
                    PictureUri = x[5],
                    Price = decimal.Parse(x[6], NumberStyles.Currency, CultureInfo.InvariantCulture)                    
                });
        }

    }
}