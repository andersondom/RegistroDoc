using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RegistroDoc.IdentityHub.Data;

public class IdentityHubDbContext
    : IdentityDbContext<IdentityHubUser, IdentityRole<Guid>, Guid>
{
    public IdentityHubDbContext(
        DbContextOptions<IdentityHubDbContext> options)
        : base(options)
    {
    }
}
