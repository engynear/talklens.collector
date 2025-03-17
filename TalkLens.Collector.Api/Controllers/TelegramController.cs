using Microsoft.AspNetCore.Mvc;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Api.Controllers
{
    [Route("telegram")]
    public class TelegramController : BaseApiController
    {
        private readonly ITelegramSessionService _telegramService;

        public TelegramController(ITelegramSessionService telegramService)
        {
            _telegramService = telegramService;
        }

        [HttpGet("contacts")]
        public async Task<ActionResult<List<TelegramContactResponse>>> GetContacts(
            [FromQuery] string sessionId,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            try
            {
                var contacts = await _telegramService.GetContactsAsync(
                    userId, 
                    sessionId,
                    cancellationToken);
                return Ok(contacts);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
} 