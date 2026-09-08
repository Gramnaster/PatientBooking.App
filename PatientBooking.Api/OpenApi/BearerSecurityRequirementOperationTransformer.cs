using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PatientBooking.Api.OpenApi;

// Requires auth only when IAuthorizeData exists without IAllowAnonymous - matches this app's no-fallback authorization model.
internal sealed class BearerSecurityRequirementOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuth)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];

        // OpenApiSecuritySchemeReference lacks value equality, but this dictionary is write-once and never looked up by key.
#pragma warning disable SS004
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
        });
#pragma warning restore SS004

        return Task.CompletedTask;
    }
}
