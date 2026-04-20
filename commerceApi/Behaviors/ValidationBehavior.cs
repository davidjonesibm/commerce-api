using FluentValidation;
using MediatR;

namespace commerceApi.Behaviors;

// LEARNING NOTE: Validation Pipeline Behavior
//
// This behavior runs BEFORE every handler. It:
// 1. Finds all FluentValidation validators registered for the current request type
// 2. Runs them all
// 3. If any fail → throws ValidationException (handler never executes)
// 4. If all pass → calls next() to continue to the handler
//
// WHY IS THIS POWERFUL?
// - You write validators ONCE, and they run for EVERY command automatically
// - No validation logic in handlers or controllers
// - Validators are their own classes — easy to test individually
// - Adding a new validation rule is just adding a new Validator class
//
// HOW DOES IT FIND VALIDATORS?
// FluentValidation registers all validators via DI (assembly scanning).
// This behavior injects IEnumerable<IValidator<TRequest>> — DI provides all matches.
// If no validators exist for a request type, the enumerable is empty → skip validation.
//
// EXAMPLE FLOW:
//   1. mediator.Send(new CreateProductCommand("Widget", null, 9.99m, 10))
//   2. LoggingBehavior runs (logs the request)
//   3. ValidationBehavior runs ← WE ARE HERE
//      a. DI injects IEnumerable<IValidator<CreateProductCommand>>
//      b. Finds CreateProductValidator (registered via assembly scanning)
//      c. Runs CreateProductValidator.Validate(command)
//      d. No errors → calls next()
//   4. CreateProductHandler runs (does the real work)
//
// WHAT IF VALIDATION FAILS?
//   3c. Runs CreateProductValidator.Validate(command)
//   3d. Price is -5 → ValidationException thrown!
//   3e. Handler NEVER runs. Exception propagates up through LoggingBehavior.
//       A global exception handler (middleware) catches it and returns 400 Bad Request.
//
// REGISTRATION (done in Program.cs — not here):
//   services.AddValidatorsFromAssembly(typeof(Program).Assembly);  // finds all AbstractValidator<T>
//   services.AddMediatR(cfg => {
//       cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
//       cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));         // registers this behavior
//   });

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // LEARNING NOTE: IEnumerable<IValidator<TRequest>> — Injecting MULTIPLE Validators
    //
    // A single request type can have MULTIPLE validators. DI collects them all into
    // an IEnumerable. For example, CreateProductCommand might have:
    //   - CreateProductValidator (basic field rules)
    //   - CreateProductBusinessValidator (checks for duplicate names in DB)
    //
    // If no validators are registered for a given TRequest, the enumerable is simply empty.
    // This means queries with no validation rules pass straight through — no overhead.
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // LEARNING NOTE: Early Exit — No Validators? Skip Validation Entirely.
        //
        // This is important for performance. Query handlers typically have no validators,
        // so we avoid creating a ValidationContext or doing any LINQ work for them.
        if (!_validators.Any())
            return await next();

        // LEARNING NOTE: ValidationContext<TRequest> — The Input to FluentValidation
        //
        // FluentValidation validators don't take the raw object directly. They take a
        // ValidationContext that wraps it. This context can carry extra data (like
        // "is this an update or a create?") but for most cases, this simple wrapper is enough.
        var context = new ValidationContext<TRequest>(request);

        // LEARNING NOTE: Running All Validators and Collecting Failures
        //
        // Step by step:
        //   1. _validators.Select(v => v.Validate(context))
        //      → Runs each validator and gets a ValidationResult
        //
        //   2. .SelectMany(result => result.Errors)
        //      → Flattens all error lists into a single list
        //      → A validator with no errors returns an empty list (not null)
        //
        //   3. .Where(f => f != null)
        //      → Safety filter (shouldn't be needed, but defensive coding)
        //
        //   4. .ToList()
        //      → Materializes the results so we can check .Count
        //
        // WHY NOT ValidateAsync?
        // We use synchronous Validate() here because our validators contain only
        // simple in-memory rules (NotEmpty, MaximumLength, GreaterThan, etc.).
        // If you had async rules (e.g., checking a database for uniqueness),
        // you'd use ValidateAsync instead.
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(result => result.Errors)
            .Where(f => f != null)
            .ToList();

        // LEARNING NOTE: Short-Circuit on Validation Failure
        //
        // If ANY rule from ANY validator fails, we throw a ValidationException.
        // This exception contains ALL failures (not just the first one), so the
        // client gets a complete picture of what's wrong in a single response.
        //
        // The handler NEVER runs — we save the cost of database calls, external
        // API calls, etc. for requests we know are invalid.
        //
        // FluentValidation's ValidationException is caught by middleware/exception
        // handlers and typically converted to a 400 Bad Request with error details.
        if (failures.Count > 0)
            throw new ValidationException(failures);

        // LEARNING NOTE: All validators passed → continue to the next step in the pipeline.
        // This might be another behavior (like TransactionBehavior) or the actual handler.
        return await next();
    }
}
