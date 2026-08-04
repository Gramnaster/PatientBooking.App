using PatientBooking.Api.Common.Enums;

namespace PatientBooking.Api.Common.Results;

// Represents outcome of operation without returning a value
// Letting code signal errors without throwing exceptions.
public readonly record struct Result
{
    // True if operation succeeds; false otherwise
    public bool IsSuccess { get; }

    // Errors describing why the operation failed. Empty on success
    public ResultError[] Errors { get; }

    // Private constructor so instances can only be created through
    // Success/Failure factory methods below, keeping construction consistent and valid
    private Result(bool isSuccess, ResultError[] errors)
        => (IsSuccess, Errors) = (isSuccess, errors);

    // Record structs compare array-typed members by reference, not content
    // Override so two Results with equal-but-distinct Errors arrays compare equal
    public bool Equals(Result other)
        => IsSuccess == other.IsSuccess && Errors.AsSpan().SequenceEqual(other.Errors);

    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(IsSuccess);
        foreach (ResultError error in Errors)
        {
            hash.Add(error);
        }

        return hash.ToHashCode();
    }

    // Creates a successful result with no errors
    public static Result Success()
        => new(isSuccess: true, errors: []);

    // Creates a failed result from the supplied errors
    // 'params' lets callers pass any number of errors as individual arguments
    public static Result Failure(params ResultError[] errors)
        => new(isSuccess: false, errors: errors);

    // Single-message codes that represent one business-rule statement
    public static Result NotFound(string? message = null)
        => Failure(new ResultError(nameof(ErrorCodes.NotFound), message ?? "Resource not found."));

    public static Result Conflict(string? message = null)
        => Failure(new ResultError(nameof(ErrorCodes.Conflict), message ?? "Resource has conflicts."));

    public static Result Unprocessable(string? message = null)
        => Failure(new ResultError(nameof(ErrorCodes.Unprocessable), message ?? "Request is unprocessable."));

    public static Result Forbid(string? message = null)
        => Failure(new ResultError(nameof(ErrorCodes.Forbid), message ?? "Request is forbidden."));

    public static Result Unauthorized(string? message = null)
        => Failure(new ResultError(nameof(ErrorCodes.Unauthorized), message ?? "Authorization failed."));

    // Multi-errors where several failures can surface at once
    public static Result BadRequest(params ResultError[] errors)
        => Failure(errors);

    public static Result Validation(params ResultError[] errors)
        => Failure(errors);

    // Merges several results into one: if any failed, returns a single Failure
    // aggregating all their errors: otherwise returns Success.
    public static Result Combine(params Result[] results)
        => results.Any(r => !r.IsSuccess)
            ? Failure(results
                .Where(r => !r.IsSuccess)   // Keep only failed results
                .SelectMany(r => r.Errors)   // Flatten error arrays into one sequence
                .ToArray())
            : Success();
}

// Generic version of Result, but carries a value (T) when the operation succeeds
// Use for operations that produce something...
// Use non-generic Result for void operations that only succeed or fail
public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ResultError[] Errors { get; }

    // Private constructor so instances can only be created through the
    // Success/Failure factory methods below, keeping construction consistent and valid
    private Result(bool isSuccess, T? value, ResultError[] errors)
        => (IsSuccess, Value, Errors) = (isSuccess, value, errors);

    // Record structs compare array-typed members by reference, not content
    // Override so two Results with equal-but-distinct Errors arrays compare equal
    public bool Equals(Result<T> other)
        => IsSuccess == other.IsSuccess
        && EqualityComparer<T>.Default.Equals(Value, other.Value)
        && Errors.AsSpan().SequenceEqual(other.Errors);

    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(IsSuccess);
        hash.Add(Value);
        foreach (ResultError error in Errors)
        {
            hash.Add(error);
        }

        return hash.ToHashCode();
    }

    public static Result<T> Success(T value)
        => new(isSuccess: true, value: value, errors: []);

    public static Result<T> Failure(params ResultError[] errors)
        => new(isSuccess: false, value: default, errors: errors);

    // Single-message codes that represent one business-rule statement
    public static Result<T> NotFound(string? message = null)
        => Failure(new ResultError(
            nameof(ErrorCodes.NotFound),
            message ?? "Resource not found."));

    public static Result<T> Conflict(string? message = null)
    => Failure(new ResultError(nameof(ErrorCodes.Conflict), message ?? "Resource has conflicts."));

    public static Result<T> Unprocessable(string? message = null)
        => Failure(new ResultError(nameof(ErrorCodes.Unprocessable), message ?? "Request is unprocessable."));

    public static Result<T> Forbid(string? message = null)
        => Failure(new ResultError(nameof(ErrorCodes.Forbid), message ?? "Request is forbidden."));

    public static Result<T> Unauthorized(string? message = null)
        => Failure(new ResultError(nameof(ErrorCodes.Unauthorized), message ?? "Authorization failed."));

    // Multi-errors where several failures can surface at once
    public static Result<T> BadRequest(params ResultError[] errors)
        => Failure(errors);
    public static Result<T> Validation(params ResultError[] errors)
        => Failure(errors);

    // Functional Helpers
    // Let callers chain operations without repeatedly checking IsSuccess:
    // On Failure, each helper short-circuits and forwards the existing errors unchanged

    // Transforms value with function that cannot fail (T -> TResult)
    // On Success: Applies 'map' and re-wraps new value in a Result<K>
    // On Failure: Skips 'map' and passes errors straight through
    public Result<TResult> Map<TResult>(Func<T, TResult> map)
        => IsSuccess
        ? Result<TResult>
            .Success(map(Value!))
        : Result<TResult>
            .Failure(Errors);

    // Chains another operation that itself can fail (T --> Result<TResult>)
    // On Success: Runs 'next' and returns its Result directly (flattening, so no nested Result)
    // On Failure: Skips 'next' and forwards the errors
    // Use Bind (not Map) when the next step returns its own Result)
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> next)
        => IsSuccess
        ? next(Value!)
        : Result<TResult>
            .Failure(Errors);

    // Validates value against predicate
    // If result = successful && value fails check, becomes Failure carrying 'error'
    // Otherwise returned unchanged
    public Result<T> Ensure(Func<T, bool> predicate, ResultError error)
        => IsSuccess && !predicate(Value!)
        ? Failure(error)
        : this;
}