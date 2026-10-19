using Microsoft.AspNetCore.Identity;
using BlazorQuiz.Data;

namespace BlazorQuiz.Components.Account
{
    internal sealed class IdentityUserAccessor(UserManager<ApplicationUser> userManager, IdentityRedirectManager redirectManager)
    {
        public async Task<ApplicationUser> GetRequiredUserAsync(HttpContext context)
        {
            var user = await userManager.GetUserAsync(context.User);

            if (user is null)
            {
                redirectManager.RedirectToWithStatus("Account/InvalidUser", $"Kesalahan: pengguna dengan ID '{userManager.GetUserId(context.User)}' tidak dapat dimuat.", context);
            }

            return user;
        }
    }
}
