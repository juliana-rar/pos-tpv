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
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.ImageUrl).MaximumLength(300);
    }
}

public class ExtraFormValidator : AbstractValidator<ExtraFormDto>
{
    public ExtraFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
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
        RuleFor(x => x.Phone).NotEmpty();
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
        RuleFor(x => x.PrimaryColor).Matches("^#[0-9a-fA-F]{6}$").WithMessage("Primary colour must be a hex value like #6366f1.");
        RuleFor(x => x.ReceiptLegalName).MaximumLength(120);
        RuleFor(x => x.ReceiptTaxId).MaximumLength(40);
        RuleFor(x => x.ReceiptAddress).MaximumLength(250);
        RuleFor(x => x.ReceiptFooter).MaximumLength(300);
        RuleFor(x => x.ReceiptPaperWidth).Must(w => w is "58" or "80").WithMessage("Paper width must be 58 or 80 mm.");
    }
}

public class SupplierFormValidator : AbstractValidator<SupplierFormDto>
{
    public SupplierFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ContactName).MaximumLength(120);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(160).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.TaxId).MaximumLength(40);
        RuleFor(x => x.Address).MaximumLength(250);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class PurchaseLineFormValidator : AbstractValidator<PurchaseLineFormDto>
{
    public PurchaseLineFormValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public class PurchaseFormValidator : AbstractValidator<PurchaseFormDto>
{
    public PurchaseFormValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage("A supplier is required.");
        RuleFor(x => x.Reference).MaximumLength(60);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one line.");
        RuleForEach(x => x.Lines).SetValidator(new PurchaseLineFormValidator());
    }
}

public class StockAdjustFormValidator : AbstractValidator<StockAdjustFormDto>
{
    public StockAdjustFormValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.NewQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Note).MaximumLength(300);
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
