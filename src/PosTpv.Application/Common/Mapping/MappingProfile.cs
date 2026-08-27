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

        CreateMap<Extra, ExtraDto>();
        CreateMap<Extra, ExtraFormDto>()
            .ForMember(d => d.ProductIds, o => o.MapFrom(s => s.Products.Select(p => p.Id)));
        CreateMap<ExtraFormDto, Extra>()
            .ForMember(d => d.Products, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<Product, ProductDto>()
            .ForCtorParam(nameof(ProductDto.CategoryName), o => o.MapFrom(s => s.Category != null ? s.Category.Name : string.Empty));
        CreateMap<Product, ProductFormDto>()
            .ForMember(d => d.AllergenIds, o => o.MapFrom(s => s.Allergens.Select(a => a.Id)))
            .ForMember(d => d.ExtraIds, o => o.MapFrom(s => s.Extras.Select(e => e.Id)));
        CreateMap<ProductFormDto, Product>()
            .ForMember(d => d.Category, o => o.Ignore())
            .ForMember(d => d.Extras, o => o.Ignore())
            .ForMember(d => d.Allergens, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<SupplierDocument, SupplierDocumentDto>();

        CreateMap<Supplier, SupplierDto>()
            .ForCtorParam(nameof(SupplierDto.DocumentCount), o => o.MapFrom(s => s.Documents.Count))
            .ForCtorParam(nameof(SupplierDto.PurchaseCount), o => o.MapFrom(s => s.Purchases.Count));
        CreateMap<Supplier, SupplierFormDto>();
        CreateMap<SupplierFormDto, Supplier>()
            .ForMember(d => d.Documents, o => o.Ignore())
            .ForMember(d => d.Purchases, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<PurchaseLine, PurchaseLineDto>()
            .ForCtorParam(nameof(PurchaseLineDto.ProductName), o => o.MapFrom(s => s.Product.Name));
        CreateMap<Purchase, PurchaseDto>()
            .ForCtorParam(nameof(PurchaseDto.SupplierName), o => o.MapFrom(s => s.Supplier.Name));

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
