﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Ajax.Utilities;

namespace InventoryWeb.Services
{
    using System.Net;

    using ServiceStack;
    using ServiceStack.Host;

    [Api("Stock Count Service")]
    [Route("/stockcount/{StockCountId}", "GET", Summary = @"Get a stock count by ID", Notes = "GET Notes")]
    public class GetStockCount : IReturn<StockCount>
    {
        [ApiMember(Name = "StockCountId", Description = "The ID of the stock count", ParameterType = "query", DataType = "int", IsRequired = false)]
        public int? StockCountId { get; set; }
    }

    [Api("Stock Count Service")]
    [Route("/stockcount", "GET", Summary = @"Find matching stock counts", Notes = "Will find stock counts that match the criteria")]
    public class FindStockCount : IReturn<List<StockCount>>
    {
        [ApiMember(Name = "LocationId", Description = "A location id for getting stock conts", ParameterType = "query", DataType = "int", IsRequired = false)]
        public int? LocationId { get; set; }
        [ApiMember(Name = "CategoryCode", Description = "A category code for getting stock counts", ParameterType = "query", DataType = "string", IsRequired = false)]
        public string CategoryCode { get; set; }
    }

    [Api("Stock Count Service")]
    [Route("/stockcount/start", "POST", Summary = @"Start a stock count with specified data", Notes = "POST Notes")]
    public class StartStockCount : IReturn
    {
        [ApiMember(Name = "LocationId", Description = "Location for this stock count", ParameterType = "query", DataType = "int", IsRequired = true)]
        public int LocationId { get; set; }

        [ApiMember(Name = "ProductCategoryCode", Description = "Product Category for this stock count", ParameterType = "query", DataType = "string", IsRequired = true)]
        public string ProductCategoryCode { get; set; }
    }
    
    [Api("Stock Count Service")]
    [Route("/stockcount/take", "POST", Summary = @"Report the RFID tag reads for the stock count", Notes = "Send RFID reads for stock counting")]
    public class ReportStockTake : IReturn
    {
        [ApiMember(Name = "StockTake", Description = "The location and tags for a stock count", ParameterType = "body", DataType = "StockTake", IsRequired = true)]
        public StockTake StockTake { get; set; }
    }

    public class StockCountService : IService
    {
        private List<StockCount> inProgressStockCounts = StockCountProvider.InProgressStockCounts;
        private Location[] locations = StockCountProvider.Locations;
        private ProductCategory[] productCategories = StockCountProvider.ProductCategories;

        public StockCount Get(GetStockCount request)
        {
            var stockCount = inProgressStockCounts
                .FirstOrDefault(x => x.StockCountId == request.StockCountId);

            if (stockCount == null)
                throw new HttpError(HttpStatusCode.NotFound, string.Format("No stock count found with id {0}", request.StockCountId));

            return stockCount;
        }

        public List<StockCount> Get(FindStockCount request)
        {
            var matching = inProgressStockCounts.AsQueryable();
            if (request.LocationId != null)
            {
                matching = matching.Where(x => x.Location.LocationId == request.LocationId);
            }

            if (!string.IsNullOrEmpty(request.CategoryCode))
            {
                matching = matching.Where(x => x.ProductCategory.CategoryCode == request.CategoryCode);
            }

            return matching.ToList();

        }

        public object Post(StartStockCount request)
        {
            var maxId = inProgressStockCounts.Max(x => x.StockCountId);
            var location = locations.FirstOrDefault(x => x.LocationId == request.LocationId);
            var category = productCategories.FirstOrDefault(x => x.CategoryCode == request.ProductCategoryCode);
            if (location == null || category == null)
            {
                //TODO: What happens when Tesco services throw exceptions?
                throw new HttpError(HttpStatusCode.NotAcceptable, string.Format("Unacceptable location or product code"));
            }
            else
            {
                var newStockCount = new StockCount
                                              {
                                                  StockCountId = maxId + 1,
                                                  Location = location,
                                                  ProductCategory = category,
                                                  Description = string.Format("{0} - {1}", location.Name, category.CategoryName)
                                              };
                inProgressStockCounts.Add(newStockCount);
                StockCountProvider.InProgressStockCounts.Add(newStockCount);
                return new HttpResult(newStockCount.StockCountId, HttpStatusCode.Accepted);
            }
        }

        public object Post(ReportStockTake request)
        {
            var matching = inProgressStockCounts.AsQueryable();
            if (request.StockTake.LocationId != null)
            {
                matching = matching.Where(x => x.Location.LocationId == request.StockTake.LocationId);
            }

            var toUpdate = matching.FirstOrDefault();
            StockCountProvider.InProgressStockCounts.Remove(toUpdate);
            foreach (var epcProduct in request.StockTake.ProductIdentifiers)
            {
                toUpdate.RfidEventLog.RfidEvents.Add(new RfidEvent { LocationId = request.StockTake.LocationId, WorkArea = request.StockTake.WorkArea, TagIdHex = epcProduct.TagIdHex });
            }

            StockCountProvider.InProgressStockCounts.Add(toUpdate);
            return new HttpResult(0, HttpStatusCode.Accepted);
        }
    }

    public static class StockCountProvider
    {
        public static Location[] Locations = new Location[]
        {
            new Location { LocationId = 1, Name = "Baldock" },
            new Location { LocationId = 2, Name = "Stevenage" },
        };

        public static ProductCategory[] ProductCategories = new ProductCategory[]
        {
            new ProductCategory { CategoryId = 0, CategoryCode = "H7", CategoryName = "Clothing" }, 
            new ProductCategory { CategoryId = 1, CategoryCode = "H71", CategoryName = "Womens" }, 
            new ProductCategory { CategoryId = 2, CategoryCode = "H72", CategoryName = "Toddlers" }, 
            new ProductCategory { CategoryId = 3, CategoryCode = "H73", CategoryName = "Baby" }, 
            new ProductCategory { CategoryId = 4, CategoryCode = "H74", CategoryName = "Girls" }, 
            new ProductCategory { CategoryId = 5, CategoryCode = "H75", CategoryName = "Boys" }, 
            new ProductCategory { CategoryId = 6, CategoryCode = "H76", CategoryName = "Mens" }, 
            new ProductCategory { CategoryId = 7, CategoryCode = "H77", CategoryName = "Schoolwear" }, 
            new ProductCategory { CategoryId = 8, CategoryCode = "H78", CategoryName = "Footwear" }, 
            new ProductCategory { CategoryId = 9, CategoryCode = "H79", CategoryName = "Underwear" }
        };

        public static List<StockCount> InProgressStockCounts = new List<StockCount>
        {
            new StockCount { StockCountId = 1, Description = "Baldock - Clothing", Location = Locations[0], ProductCategory = ProductCategories[0] },
            new StockCount { StockCountId = 2, Description = "Baldock - Menswear", Location = Locations[0], ProductCategory = ProductCategories[6] },
            new StockCount { StockCountId = 3, Description = "Stevanage - Clothing", Location = Locations[1], ProductCategory = ProductCategories[0] },
            new StockCount { StockCountId = 4, Description = "Stevanage - Boys", Location = Locations[1], ProductCategory = ProductCategories[5] }
        };
    }
}
