using StoreService.Core.Interfaces.Services;
using StoreService.Core.Mappings;
using Microsoft.Extensions.DependencyInjection;

namespace StoreService.Core.ServiceRegistration;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(MappingProfile));
        services.AddScoped<IPurchasingService, Services.PurchasingService>();
        services.AddScoped<IStockService, Services.StockService>();
        services.AddScoped<Interfaces.Services.IItemPhotoService, Services.ItemPhotoService>();
        return services;
    }
}
