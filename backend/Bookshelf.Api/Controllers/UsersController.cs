using Bookshelf.Api.Extensions;
using Bookshelf.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var profile = await userService.GetProfileAsync(User.GetUserId());
        return Ok(profile);
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateUserRequest request)
    {
        var profile = await userService.UpdateProfileAsync(User.GetUserId(), request);
        return Ok(profile);
    }
}
