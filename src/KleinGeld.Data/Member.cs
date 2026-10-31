using Microsoft.AspNetCore.Identity;

namespace KleinGeld.Data;

/// <summary>A person who has been approved to join and can log in.</summary>
public sealed class Member : IdentityUser<Guid>
{
    public Member()
    {
        Id = Guid.CreateVersion7();
    }
}
