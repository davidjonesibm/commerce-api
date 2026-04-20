using FluentValidation;
using commerceApi.Features.Products.Commands;

namespace commerceApi.Features.Products.Validators;

// LEARNING NOTE: FluentValidation — Readable Validation Rules
//
// Instead of cluttering your handler with imperative checks like:
//   if (string.IsNullOrEmpty(product.Name)) throw new Exception("Name required");
//   if (product.Price <= 0) throw new Exception("Price must be positive");
//
// We write declarative rules that read like English:
//   RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
//   RuleFor(x => x.Price).GreaterThan(0);
//
// Benefits:
// - Reads like English — easy to understand at a glance
// - Built-in validators for common scenarios (NotEmpty, MaxLength, EmailAddress, etc.)
// - Automatic error messages (you can override them with .WithMessage())
// - Each validator is a standalone class — easy to unit test
// - Works with our ValidationBehavior to run BEFORE handlers automatically
//
// HOW IT CONNECTS:
//   1. Program.cs calls: services.AddValidatorsFromAssembly(...)
//      → FluentValidation scans for all classes that extend AbstractValidator<T>
//      → Registers CreateProductValidator as IValidator<CreateProductCommand>
//
//   2. When mediator.Send(new CreateProductCommand(...)) is called:
//      → ValidationBehavior receives IEnumerable<IValidator<CreateProductCommand>>
//      → Finds this validator
//      → Runs it
//      → Throws if any rules fail

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        // LEARNING NOTE: RuleFor — Define a Rule for a Single Property
        //
        // RuleFor(x => x.Name) targets the Name property of CreateProductCommand.
        // You can chain multiple validators — they ALL must pass:
        //   .NotEmpty()         → must not be null, empty, or whitespace
        //   .MaximumLength(200) → must not exceed 200 characters
        //
        // .WithMessage() overrides the default error message FluentValidation would generate.
        // Default for NotEmpty would be: "'Name' must not be empty."
        // We provide a more user-friendly message instead.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");

        // LEARNING NOTE: GreaterThan(0) — Numeric Comparison Validator
        //
        // For decimal properties, GreaterThan checks that the value is strictly > 0.
        // This prevents free products and negative prices.
        // Other numeric validators: LessThan, GreaterThanOrEqualTo, LessThanOrEqualTo,
        //                          InclusiveBetween, ExclusiveBetween
        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero");

        // LEARNING NOTE: GreaterThanOrEqualTo(0) — Allows Zero
        //
        // Stock can be zero (out of stock) but can't be negative.
        // GreaterThanOrEqualTo(0) ensures value >= 0.
        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity cannot be negative");
    }
}
