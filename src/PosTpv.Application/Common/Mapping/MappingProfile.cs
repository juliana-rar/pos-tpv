using AutoMapper;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Common.Mapping;

/// <summary>AutoMapper configuration for the straightforward entity/DTO conversions.</summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>();

        CreateMap<Category, CategoryDto>()
            .ForCtorParam(nameof(CategoryDto.ProductCount), o => o.MapFrom(s => s.Products.Count));
        CreateMap<Category, CategoryFormDto>();
        CreateMap<CategoryFormDto, Category>()
            .ForMember(d => d.Products, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<Product, ProductDto>()
            .ForCtorParam(nameof(ProductDto.CategoryName), o => o.MapFrom(s => s.Category != null ? s.Category.Name : string.Empty))
            .ForCtorParam(nameof(ProductDto.HasExtras), o => o.MapFrom(s => s.Extras != null && s.Extras.Count > 0));
        CreateMap<Product, ProductFormDto>();
        CreateMap<ProductFormDto, Product>()
            .ForMember(d => d.Category, o => o.Ignore())
            .ForMember(d => d.Extras, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        CreateMap<Reservation, ReservationDto>()
            .ForCtorParam(nameof(ReservationDto.TableName), o => o.MapFrom(s => s.Table != null ? s.Table.Name : null));
        CreateMap<Reservation, ReservationFormDto>();
        CreateMap<ReservationFormDto, Reservation>()
            .ForMember(d => d.Table, o => o.Ignore())
            .ForMember(d => d.Customer, o => o.Ignore())
            .ForMember(d => d.CustomerId, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());
    }
}
