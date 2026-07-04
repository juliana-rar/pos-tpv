using PosTpv.Domain.Enums;

namespace PosTpv.Application.DTOs;

public record UserDto(int Id, string Username, string FullName, UserRole Role);

public record ExtraDto(int Id, string Name, decimal Price);

public record CategoryDto(int Id, string Name, string? Icon, string Color, string? ImageUrl, int DisplayOrder, bool IsVisible, int ProductCount = 0);

public class CategoryFormDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string Color { get; set; } = "#6366f1";
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}

public record ProductDto(
    int Id, string Name, string? Description, decimal Price, decimal VatRate,
    string Color, string? ImageUrl, int DisplayOrder, bool IsVisible, bool IsAvailable,
    int PreparationMinutes, string? Ingredients, string? Allergens, int CategoryId, string CategoryName,
    bool HasExtras);

public class ProductFormDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal VatRate { get; set; } = 10m;
    public string Color { get; set; } = "#6366f1";
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public int PreparationMinutes { get; set; }
    public string? Ingredients { get; set; }
    public string? Allergens { get; set; }
    public int CategoryId { get; set; }
}
