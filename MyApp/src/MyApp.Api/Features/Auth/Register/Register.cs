using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Common.Http;
using MyApp.Api.Common.Results;
using MyApp.Api.Domain;
using MyApp.Api.Persistence.Identity;

namespace MyApp.Api.Features.Auth.Register;

public sealed record RegisterCommand(string Email, string Password) : ICommand<RegisterResponse>;

public sealed record RegisterResponse(Guid Id, string Email);

internal sealed class RegisterHandler(UserManager<ApplicationUser> userManager)
    : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public async Task<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || !MailAddress.TryCreate(command.Email, out _))
        {
            return Error.Validation("Auth.EmailInvalid", "A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return Error.Validation("Auth.PasswordRequired", "Password is required.");
        }

        var user = new ApplicationUser { UserName = command.Email, Email = command.Email };

        var created = await userManager.CreateAsync(user, command.Password);
        if (!created.Succeeded)
        {
            return ToError(created);
        }

        var roleAssigned = await userManager.AddToRoleAsync(user, Roles.User);
        if (!roleAssigned.Succeeded)
        {
            // Don't leave a user without a role behind.
            await userManager.DeleteAsync(user);
            return Error.Failure("Auth.RoleAssignmentFailed", "The account could not be created.");
        }

        return new RegisterResponse(user.Id, user.Email!);
    }

    private static Error ToError(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Error.Conflict("Auth.EmailTaken", "An account with this email already exists.");
        }

        return Error.Validation("Auth.RegistrationFailed", string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}

internal sealed class RegisterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/register", async (
                RegisterCommand command,
                ICommandHandler<RegisterCommand, RegisterResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(command, cancellationToken);
                return result.Match(response => Results.Created($"/api/users/{response.Id}", response));
            })
            .AllowAnonymous()
            .WithName("Register")
            .WithTags("Auth");
}
