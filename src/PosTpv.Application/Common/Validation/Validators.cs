using FluentValidation;
using PosTpv.Application.DTOs;

namespace PosTpv.Application.Common.Validation;

public class CategoryFormValidator : AbstractValidator<CategoryFormDto>
{
    public CategoryFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(9);
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
