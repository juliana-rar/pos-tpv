using FluentValidation;
using PosTpv.Application.DTOs;

namespace PosTpv.Application.Common.Validation;

public class CategoryFormValidator : AbstractValidator<CategoryFormDto>
{
    public CategoryFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(9);
        RuleFor(x => x.ImageUrl).MaximumLength(300);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class ProductFormValidator : AbstractValidator<ProductFormDto>
{
    public ProductFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VatRate).InclusiveBetween(0, 100);
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("A category is required.");
        RuleFor(x => x.PreparationMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageUrl).MaximumLength(300);
    }
}

public class AllergenFormValidator : AbstractValidator<AllergenFormDto>
{
    public AllergenFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ImageUrl).MaximumLength(300);
    }
}

public class TableFormValidator : AbstractValidator<TableFormDto>
{
    public TableFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Seats).InclusiveBetween(1, 30);
    }
}

public class ReservationFormValidator : AbstractValidator<ReservationFormDto>
{
    public ReservationFormValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.PartySize).InclusiveBetween(1, 50);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(15, 600);
    }
}

public class UserFormValidator : AbstractValidator<UserFormDto>
{
    public UserFormValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Pin).NotEmpty().WithMessage("A PIN is required for new users.").When(x => x.Id == 0);
        RuleFor(x => x.Pin).Matches("^[0-9]{4,8}$").WithMessage("PIN must be 4 to 8 digits.").When(x => !string.IsNullOrEmpty(x.Pin));
    }
}

public class AppSettingsFormValidator : AbstractValidator<AppSettingsFormDto>
{
    public AppSettingsFormValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LunchEnd).GreaterThan(x => x.LunchStart).WithMessage("Lunch end must be after lunch start.");
        RuleFor(x => x.DinnerEnd).GreaterThan(x => x.DinnerStart).WithMessage("Dinner end must be after dinner start.");
    }
}

public class AddItemRequestValidator : AbstractValidator<AddItemRequest>
{
    public AddItemRequestValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Comment).MaximumLength(250);
    }
}
