using FluentValidation;
using commerceApi.Features.Customers.Commands;

namespace commerceApi.Features.Customers.Validators;

// LEARNING NOTE: Customer Validation — Same Pattern, Different Rules
//
// Every validator follows the same structure:
//   1. Extend AbstractValidator<T> where T is the command/query you're validating
//   2. Define rules in the constructor
//   3. FluentValidation + our ValidationBehavior handles the rest
//
// You NEVER call this validator manually. The pipeline finds it automatically:
//   mediator.Send(new CreateCustomerCommand("", "", "not-an-email"))
//     → ValidationBehavior finds CreateCustomerValidator via DI
//     → Runs all rules
//     → Throws ValidationException with ALL three failures at once
//     → Handler never executes
//     → Exception handler returns 400 with error details

public class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        // LEARNING NOTE: String Validation — NotEmpty + MaximumLength
        //
        // NotEmpty() catches: null, "", and "   " (whitespace-only strings).
        // MaximumLength(100) sets an upper bound to match the database column size.
        //
        // Tip: Always align MaximumLength with your database column's VARCHAR/TEXT limit.
        // If your DB column is VARCHAR(100), set MaximumLength(100) here to catch
        // oversized strings BEFORE they hit the database and cause a DB-level error.
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");

        // LEARNING NOTE: EmailAddress() — Built-in Email Format Validator
        //
        // FluentValidation includes an EmailAddress() validator that checks the string
        // matches a valid email format (e.g., "user@example.com").
        //
        // What it checks:
        //   ✓ "user@example.com"        → valid
        //   ✓ "user.name@domain.co.uk"  → valid
        //   ✗ "not-an-email"            → invalid
        //   ✗ "@missing-local.com"       → invalid
        //   ✗ "user@"                    → invalid
        //
        // What it does NOT check:
        //   ✗ Whether the email actually exists (that requires sending a verification email)
        //   ✗ Whether the domain has MX records (that requires DNS lookup)
        //   ✗ Disposable/temporary email providers
        //
        // For most APIs, format validation is sufficient. If you need stricter checks,
        // you'd add an async validator rule that queries an email verification service.
        //
        // IMPORTANT: We chain NotEmpty() BEFORE EmailAddress() because EmailAddress()
        // would pass for null/empty strings (no format to check). NotEmpty() catches
        // missing values first.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required")
            .EmailAddress().WithMessage("A valid email address is required");
    }
}
