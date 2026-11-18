using Microsoft.AspNetCore.Identity;

namespace MyApp.Api.Persistence.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser() => Id = Guid.NewGuid();
}
