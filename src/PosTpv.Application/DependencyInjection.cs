using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PosTpv.Application.Common;
using PosTpv.Application.Services;

namespace PosTpv.Application;

/// <summary>Registers AutoMapper, FluentValidation and the application services.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddAutoMapper(cfg => { }, assembly);
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAllergenService, AllergenService>();
        services.AddScoped<IExtraService, ExtraService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IFloorDecorService, FloorDecorService>();
        services.AddScoped<IFloorZoneService, FloorZoneService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddSingleton<AppSettingsCache>();

        return services;
    }
}
