using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace TalkLens.Collector.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public abstract class BaseApiController : ControllerBase
    {
        /// <summary>
        /// Получает ID пользователя из токена
        /// </summary>
        /// <returns>ID пользователя или string.Empty если не найден</returns>
        protected string GetUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        /// <summary>
        /// Получает имя пользователя из токена
        /// </summary>
        /// <returns>Имя пользователя или string.Empty если не найдено</returns>
        protected string GetUsername()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        }

        /// <summary>
        /// Получает время выдачи токена
        /// </summary>
        /// <returns>Время выдачи токена или DateTimeOffset.MinValue если не найдено</returns>
        protected DateTimeOffset GetTokenIssuedAt()
        {
            var issuedAtClaim = User.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
            return !string.IsNullOrEmpty(issuedAtClaim) && 
                   long.TryParse(issuedAtClaim, out var timestamp)
                ? DateTimeOffset.FromUnixTimeSeconds(timestamp)
                : DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Получает ID токена
        /// </summary>
        /// <returns>ID токена или string.Empty если не найден</returns>
        protected string GetTokenId()
        {
            return User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? string.Empty;
        }

        /// <summary>
        /// Проверяет, является ли пользователь администратором
        /// </summary>
        protected bool IsAdmin()
        {
            return User?.IsInRole("admin") ?? false;
        }

        /// <summary>
        /// Возвращает стандартный ответ для неавторизованного доступа
        /// </summary>
        protected IActionResult UnauthorizedResponse(string message = "Unauthorized access")
        {
            return Unauthorized(new { message });
        }

        /// <summary>
        /// Возвращает стандартный ответ для запрещенного доступа
        /// </summary>
        protected IActionResult ForbiddenResponse(string message = "Access forbidden")
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message });
        }
    }
} 