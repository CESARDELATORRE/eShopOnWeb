using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Identity
{
    public class AppIdentityDbContextSeed
    {
        public static async Task SeedAsync(UserManager<ApplicationUser> userManager)
        {
            var defaultUser = new ApplicationUser { UserName = "demouser@microsoft.com", Email = "demouser@microsoft.com", Id = "b0b3d87a-a904-46ac-8bd4-fd561a5c2dd3" };
            await userManager.CreateAsync(defaultUser, "Pass@word1");

            var ankit = new ApplicationUser { UserName = "ankit@microsoft.com", Email = "ankit@microsoft.com", Id = "6e892e44-1c04-4e3b-b55e-fb75a95831fa" };
            await userManager.CreateAsync(ankit, "Pass@word1");

        }
    }
}
