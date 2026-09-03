using Microsoft.AspNetCore.Mvc;
using PatientBooking.Api.Common.Enums;
using PatientBooking.Api.Common.Results;

namespace PatientBooking.Api.Controllers;

public abstract class BaseApiController : ControllerBase
{
    // Call the service, translate its Result into HTTP Response
    // Success -> 200 + body; failure + mapped ProblemDetails
    protected ActionResult<T> ToActionResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : MapErrorsToResponse(result.Errors);

    // Success -> 204 (Update/delete has no body); failure + mapped ProblemDetails
    protected ActionResult ToActionResult(Result result) =>
        result.IsSuccess ? NoContent() : MapErrorsToResponse(result.Errors);

    // Maps the first Result error's Code to a status code, always as RFC 0457 ProblemDetails
    // So every error response shares one shape
    protected ObjectResult MapErrorsToResponse(ResultError[] errors)
    {
        // No detail -> unexpected failure; Problem() with no args = 500 ProblemDetails
        if (errors is null || errors.Length == 0)
        {
            return Problem(
                detail: "No error details provided",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "An error occured"
            );
        }

        // First error drives the status; its Description becomes the ProblemDetails detail
        ResultError e = errors[0];

        return e.Code switch
        {
            nameof(ErrorCodes.NotFound) => Problem(detail: e.Description, statusCode: StatusCodes.Status404NotFound),
            nameof(ErrorCodes.BadRequest) =>
                Problem(detail: e.Description, statusCode: StatusCodes.Status400BadRequest),
            nameof(ErrorCodes.Validation) =>
                Problem(detail: e.Description, statusCode: StatusCodes.Status400BadRequest),
            nameof(ErrorCodes.Forbid) => Problem(detail: e.Description, statusCode: StatusCodes.Status403Forbidden),
            nameof(ErrorCodes.Conflict) => Problem(detail: e.Description, statusCode: StatusCodes.Status409Conflict),
            nameof(ErrorCodes.Unprocessable) =>
                Problem(detail: e.Description, statusCode: StatusCodes.Status422UnprocessableEntity),
            nameof(ErrorCodes.Unauthorized) =>
                Problem(detail: e.Description, statusCode: StatusCodes.Status401Unauthorized),
            // Unrecognized code -> aggregate all descriptions under generic 500
            _ =>
                Problem(
                    detail: string.Join("; ", errors.Select(x => x.Description)),
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: e.Code
                ),
        };
    }
}
