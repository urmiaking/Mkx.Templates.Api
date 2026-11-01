using Microsoft.AspNetCore.Identity;
namespace Mkx.Templates.Domain.Identity;
public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public ICollection<RefreshSession> Sessions { get; set; } = new List<RefreshSession>();
}
