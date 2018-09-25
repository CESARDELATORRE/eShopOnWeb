using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.RazorPages.Interfaces;
using Microsoft.eShopWeb.RazorPages.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.eShopWeb.Infrastructure.Data;

namespace Microsoft.eShopWeb.RazorPages.Services
{
    /// <summary>
    /// This is a UI-specific service so belongs in UI project. It does not contain any business logic and works
    /// with UI-specific types (view models and SelectListItem types).
    /// </summary>
    public class CatalogService : ICatalogService
    {
        private readonly ILogger<CatalogService> _logger;
        private readonly IRepository<CatalogItem> _itemRepository;
        private readonly IAsyncRepository<CatalogBrand> _brandRepository;
        private readonly IAsyncRepository<CatalogType> _typeRepository;
        private readonly IUriComposer _uriComposer;
        private readonly ICatalogRepository catalogRepository;
        private readonly IProductRecommendationService productRecommendationService;
        private readonly IDataSetReaderService dataSetReaderService;

        public CatalogService(
            ILoggerFactory loggerFactory,
            IRepository<CatalogItem> itemRepository,
            IAsyncRepository<CatalogBrand> brandRepository,
            IAsyncRepository<CatalogType> typeRepository,
            IUriComposer uriComposer,
            ICatalogRepository catalogRepository,
            IProductRecommendationService productRecommendationService,
            IDataSetReaderService dataSetReaderService)
        {
            _logger = loggerFactory.CreateLogger<CatalogService>();
            _itemRepository = itemRepository;
            _brandRepository = brandRepository;
            _typeRepository = typeRepository;
            _uriComposer = uriComposer;
            this.catalogRepository = catalogRepository;
            this.productRecommendationService = productRecommendationService;
            this.dataSetReaderService = dataSetReaderService;
        }

        public async Task<CatalogIndexViewModel> GetCatalogItems(int pageIndex, int itemsPage, int? brandId, int? typeId, string username, int recommendations)
        {
            _logger.LogInformation("GetCatalogItems called.");

            var filterSpecification = new CatalogFilterSpecification(brandId, typeId);
            var root = _itemRepository.List(filterSpecification);

            var totalItems = root.Count();

            var itemsOnPage = root
                .Skip(itemsPage * pageIndex)
                .Take(itemsPage)
                .ToList();

            itemsOnPage.ForEach(x =>
            {
                x.PictureUri = _uriComposer.ComposePicUri(x.PictureUri);
            });

            var vm = new CatalogIndexViewModel()
            {
                CatalogItems = itemsOnPage.Select(i => new CatalogItemViewModel()
                {
                    Id = i.Id,
                    Name = i.Name,
                    PictureUri = i.PictureUri,
                    Price = i.Price
                }),
                Brands = await GetBrands(),
                Types = await GetTypes(),
                BrandFilterApplied = brandId ?? 0,
                TypesFilterApplied = typeId ?? 0,
                PaginationInfo = new PaginationInfoViewModel()
                {
                    ActualPage = pageIndex,
                    ItemsPerPage = itemsOnPage.Count,
                    TotalItems = totalItems,
                    TotalPages = int.Parse(Math.Ceiling(((decimal)totalItems / itemsPage)).ToString())
                }
            };

            if (pageIndex == 0 && !String.IsNullOrEmpty(username))
            {
                //Get Recommended products for the authenticated user
                vm.RecommendedCatalogItems = (GetRecommendations(username, recommendations)).ToArray();

                //Get a few bought products in the past by the authenticated user
                vm.BoughtCatalogItems = (this.GetSelectedBoughtItemsByUser(username, 3)).ToArray();
            }
            else
            {
                vm.RecommendedCatalogItems = Enumerable.Empty<CatalogItemViewModel>();
                vm.BoughtCatalogItems = Enumerable.Empty<CatalogItemViewModel>();
            }

            vm.PaginationInfo.Next = (vm.PaginationInfo.ActualPage == vm.PaginationInfo.TotalPages - 1) ? "is-disabled" : "";
            vm.PaginationInfo.Previous = (vm.PaginationInfo.ActualPage == 0) ? "is-disabled" : "";

            return vm;
        }

        private IEnumerable<CatalogItemViewModel> GetRecommendations(string user, int recommendationsInPage)
        {
            var productIds = catalogRepository.GetAllProductIds().ToArray();
            var recommendations = productRecommendationService.GetRecommendationsForUser(user, productIds, recommendationsInPage);
            var productRecommendations = catalogRepository.GetAllProducts(recommendations.Select(c => int.Parse(c)), recommendationsInPage);
            return productRecommendations.Select(i => new CatalogItemViewModel()
            {
                Id = i.Id,
                Name = i.Name,
                PictureUri = _uriComposer.ComposePicUri(i.PictureUri),
                Price = i.Price
            });
        }

        private IEnumerable<CatalogItemViewModel> GetSelectedBoughtItemsByUser(string user, int numberOfBoughtProductsInPage)
        {
            var productIds = catalogRepository.GetAllProductIds().ToArray();
            var boughtProductsIDsList = dataSetReaderService.GetProductsBoughtByUser(user, productIds, numberOfBoughtProductsInPage);

            var productsBoughtToShow = catalogRepository.GetAllProducts(boughtProductsIDsList.Select(c => int.Parse(c)), numberOfBoughtProductsInPage);
            return productsBoughtToShow.Select(i => new CatalogItemViewModel()
            {
                Id = i.Id,
                Name = i.Name,
                PictureUri = _uriComposer.ComposePicUri(i.PictureUri),
                Price = i.Price
            });




            //To swap.. --------------------------------------
            //var recommendations = productRecommendationService.GetRecommendationsForUser(user, productIds, numberOfBoughtProductsInPage);
            //var productRecommendations = catalogRepository.GetAllProducts(recommendations.Select(c => int.Parse(c)), numberOfBoughtProductsInPage);
            //return productRecommendations.Select(i => new CatalogItemViewModel()
            //{
            //    Id = i.Id,
            //    Name = i.Name,
            //    PictureUri = _uriComposer.ComposePicUri(i.PictureUri),
            //    Price = i.Price
            //});
        }

        public async Task<IEnumerable<SelectListItem>> GetBrands()
        {
            _logger.LogInformation("GetBrands called.");
            var brands = await _brandRepository.ListAllAsync();

            var items = new List<SelectListItem>
            {
                new SelectListItem() { Value = null, Text = "All", Selected = true }
            };
            foreach (CatalogBrand brand in brands)
            {
                items.Add(new SelectListItem() { Value = brand.Id.ToString(), Text = brand.Brand });
            }

            return items;
        }

        public async Task<IEnumerable<SelectListItem>> GetTypes()
        {
            _logger.LogInformation("GetTypes called.");
            var types = await _typeRepository.ListAllAsync();
            var items = new List<SelectListItem>
            {
                new SelectListItem() { Value = null, Text = "All", Selected = true }
            };
            foreach (CatalogType type in types)
            {
                items.Add(new SelectListItem() { Value = type.Id.ToString(), Text = type.Type });
            }

            return items;
        }
    }
}
