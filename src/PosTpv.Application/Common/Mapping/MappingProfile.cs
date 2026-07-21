using AutoMapper;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Common.Mapping;

/// <summary>AutoMapper configuration for the straightforward entity/DTO conversions.</summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForCtorParam(nameof(UserDto.IsLocked), o => o.MapFrom(s => s.LockedUntil != null && s.LockedUntil > DateTime.UtcNow));
        CreateMap<User, UserFormDto>()
            .ForMember(d => d.Pin, o => o.Ignore());
        CreateMap<UserFormDto, User>()
            .ForMember(d => d.PasswordHash, o => o.Ignore())
            .ForMember(d => d.FailedLoginAttempts, o => o.Ignore())
            .ForMember(d => d.LockedUntil, o => o.Ignore())
            .ForMember(d => d.Orders, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<AppSetting, AppSettingsDto>();
        CreateMap<AppSetting, AppSettingsFormDto>();
        CreateMap<AppSettingsFormDto, AppSetting>()
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<Category, CategoryDto>()
            .ForCtorParam(nameof(CategoryDto.ProductCount), o => o.MapFrom(s => s.Products.Count));
        CreateMap<Category, CategoryFormDto>();
        CreateMap<CategoryFormDto, Category>()
            .ForMember(d => d.Products, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<Allergen, AllergenDto>();
        CreateMap<Allergen, AllergenFormDto>();
        CreateMap<AllergenFormDto, Allergen>()
            .ForMember(d => d.Products, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<Product, ProductDto>()
            .ForCtorParam(nameof(ProductDto.CategoryName), o => o.MapFrom(s => s.Category != null ? s.Category.Name : string.Empty))
            .ForCtorParam(nameof(ProductDto.HasExtras), o => o.MapFrom(s => s.Extras != null && s.Extras.Count > 0));
        CreateMap<Product, ProductFormDto>()
            .ForMember(d => d.AllergenIds, o => o.MapFrom(s => s.Allergens.Select(a => a.Id)));
        CreateMap<ProductFormDto, Product>()
            .ForMember(d => d.Category, o => o.Ignore())
            .ForMember(d => d.Extras, o => o.Ignore())
            .ForMember(d => d.Allergens, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<Reservation, ReservationDto>()
            .ForCtorParam(nameof(ReservationDto.TableIds), o => o.MapFrom(s => s.Tables.Select(t => t.Id)))
            .ForCtorParam(nameof(ReservationDto.TableNames), o => o.MapFrom(s => s.Tables.Select(t => t.Name)));
        CreateMap<Reservation, ReservationFormDto>()
            .ForMember(d => d.TableIds, o => o.MapFrom(s => s.Tables.Select(t => t.Id)));
        CreateMap<ReservationFormDto, Reservation>()
            .ForMember(d => d.Tables, o => o.Ignore())
            .ForMember(d => d.Customer, o => o.Ignore())
            .ForMember(d => d.CustomerId, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());
    }
}
