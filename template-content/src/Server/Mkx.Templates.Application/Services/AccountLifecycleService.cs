using Microsoft.AspNetCore.Identity;
using Mkx.Templates.Application.Abstractions;
using Mkx.Templates.Domain.Identity;
using Mkx.Templates.Shared.Contracts;

namespace Mkx.Templates.Application.Services;

public sealed class AccountLifecycleService(
    UserManager<AppUser> users, IAccountMessageSender sender) : IAccountLifecycleService
{
    public async Task<bool> RegisterAsync(RegisterAccountRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim() != request.Username ||
            string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim() != request.Email ||
            string.IsNullOrWhiteSpace(request.DisplayName))
            return false;

        var user = new AppUser
        {
            UserName = request.Username,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = false
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return false;

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await sender.SendAsync(user.Email!, "Confirm your account",
            "Enter this code in the app to confirm your account. Never share it." +
            "\n\n" + token + "\n\nValid for 20 minutes.", ct);
        return true;
    }

    public async Task<bool> ConfirmAsync(ConfirmAccountRequest request, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.Username);
        if (user is null || user.EmailConfirmed || user.TwoFactorEnabled)
            return false;
        return (await users.ConfirmEmailAsync(user, request.Token)).Succeeded;
    }

    public async Task RequestConfirmationAsync(AccountRecoveryRequest request, CancellationToken ct)
    {
        var user = await MatchedAsync(request.Username, request.Email);
        if (user is null || user.EmailConfirmed || user.TwoFactorEnabled)
            return;

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await sender.SendAsync(user.Email!, "Confirm your account",
            "Enter this confirmation code in the app. Never share it." +
            "\n\n" + token + "\n\nValid for 20 minutes.", ct);
    }

    public async Task RequestPasswordResetAsync(AccountRecoveryRequest request, CancellationToken ct)
    {
        var user = await MatchedAsync(request.Username, request.Email);
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled ||
            await users.IsLockedOutAsync(user) || await users.IsInRoleAsync(user, "Administrator"))
            return;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        await sender.SendAsync(user.Email!, "Reset your password",
            "If you requested a password reset, enter this code inside the app. Never share it." +
            "\n\n" + token + "\n\nValid for 20 minutes.", ct);
    }

    public async Task<bool> ResetPasswordAsync(ResetAccountPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.Username);
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled ||
            await users.IsLockedOutAsync(user) || await users.IsInRoleAsync(user, "Administrator"))
            return false;
        return (await users.ResetPasswordAsync(user, request.Token, request.NewPassword)).Succeeded;
    }

    private async Task<AppUser?> MatchedAsync(string name, string email)
    {
        var user = await users.FindByNameAsync(name);
        return user is not null && !string.IsNullOrEmpty(user.Email) &&
               string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase) ? user : null;
    }
}
