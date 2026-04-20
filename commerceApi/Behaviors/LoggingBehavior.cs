using System.Diagnostics;
using MediatR;

namespace commerceApi.Behaviors;

// LEARNING NOTE: Pipeline Behaviors — MediatR's "Middleware"
//
// You know how ASP.NET Core has middleware (logging, auth, CORS, etc.)?
// Pipeline Behaviors are the MediatR equivalent.
//
// EVERY request that goes through mediator.Send() passes through ALL registered behaviors.
// Think of it like a Russian nesting doll:
//
//   HTTP Request
//     → ASP.NET Middleware (logging, auth, etc.)
//       → Controller/Endpoint
//         → mediator.Send(command)
//           → LoggingBehavior (wraps the next step)    ← WE ARE HERE
//             → ValidationBehavior (wraps the next step)
//               → Actual Handler (does the real work)
//             ← ValidationBehavior (after handler)
//           ← LoggingBehavior (after handler)
//         ← mediator returns result
//       ← Controller returns response
//     ← ASP.NET Middleware
//   HTTP Response
//
// HOW IT WORKS:
// - IPipelineBehavior<TRequest, TResponse> is an open generic interface.
//   When registered as an open behavior, MediatR applies it to ALL request types.
//
// - The Handle method receives:
//     1. TRequest request     — the command/query being processed
//     2. RequestHandlerDelegate<TResponse> next — a delegate to call the NEXT step in the pipeline
//     3. CancellationToken    — for async cancellation support
//
// - The RequestHandlerDelegate<TResponse> next parameter is the NEXT step in the pipeline.
//   You call next() to continue, or throw to short-circuit (like validation failure).
//
// - The "where TRequest : notnull" constraint ensures we never get a null request.
//
// REGISTRATION (done in Program.cs — not here):
//   services.AddMediatR(cfg => {
//       cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
//       cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));   // ← registers for ALL request types
//   });

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // LEARNING NOTE: ILogger<T> — Structured Logging with Categories
    //
    // ILogger<LoggingBehavior<TRequest, TResponse>> creates a logger whose "category"
    // is the full generic type name (e.g., "commerceApi.Behaviors.LoggingBehavior<CreateProductCommand, Product>").
    // This makes it easy to filter logs by behavior + request type in production.
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // LEARNING NOTE: typeof(TRequest).Name gives us just the class name (e.g., "CreateProductCommand")
        // without the full namespace. This keeps log lines readable.
        var requestName = typeof(TRequest).Name;

        // LEARNING NOTE: Structured Logging with {@Request}
        //
        // The @ prefix in "{@Request}" tells the logger to SERIALIZE the object.
        // Without @, it would just call .ToString() (often useless).
        // With @, you get all properties logged as structured data:
        //   { Name: "Widget", Price: 9.99, StockQuantity: 100 }
        //
        // This is invaluable for debugging — you can see the EXACT request that was sent.
        // In production, be careful not to log sensitive data (passwords, tokens, etc.).
        _logger.LogInformation("▶ Handling {RequestName} {@Request}", requestName, request);

        // LEARNING NOTE: Stopwatch for Accurate Timing
        //
        // System.Diagnostics.Stopwatch uses a high-resolution timer to measure elapsed time.
        // It's more accurate than DateTime.Now math and is the standard approach for
        // performance measurement in .NET.
        //
        // We start BEFORE calling next() and stop AFTER — this measures:
        //   - All remaining behaviors in the pipeline (e.g., ValidationBehavior)
        //   - The actual handler execution
        //   - Any database calls, HTTP calls, etc. the handler makes
        //
        // This gives you a complete picture of how long each request takes end-to-end.
        var stopwatch = Stopwatch.StartNew();

        // LEARNING NOTE: await next() — The Heart of the Pipeline
        //
        // This single line does A LOT:
        //   1. Calls the next behavior in the pipeline (or the handler if this is the last behavior)
        //   2. Waits for it to complete
        //   3. Returns the handler's response
        //
        // If you DON'T call next(), the handler never runs and no response is produced.
        // This is how validation can short-circuit — throw before calling next().
        var response = await next();

        stopwatch.Stop();

        // LEARNING NOTE: We log the elapsed time in milliseconds.
        // In a real app, you might also log a warning if a request takes too long:
        //   if (stopwatch.ElapsedMilliseconds > 500)
        //       _logger.LogWarning("⚠ Slow request {RequestName} took {ElapsedMs}ms", ...);
        _logger.LogInformation(
            "◀ Handled {RequestName} in {ElapsedMs}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
