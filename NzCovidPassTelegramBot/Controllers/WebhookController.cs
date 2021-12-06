using Microsoft.AspNetCore.Mvc;
using NzCovidPassTelegramBot.Data.Bot;
using NzCovidPassTelegramBot.Services;
using Telegram.Bot.Types;

namespace NzCovidPassTelegramBot.Controllers
{
    [Route("api/webhook")]
    public class WebhookController : Controller
    {
        private readonly TelegramConfiguration _tgConfig;
        private readonly ILogger _logger;

        public WebhookController(ILogger<WebhookController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _tgConfig = configuration.GetSection("Telegram").Get<TelegramConfiguration>();
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Json("Pong");
        }

        [HttpPost("receive/{apiToken}")]
        public async Task<IActionResult> Post(
            string apiToken,
            [FromServices] ITelegramBotService botService,
            [FromBody] Update update)
        {
            if (!apiToken.Equals(_tgConfig.ApiToken, StringComparison.Ordinal))
            {
                return Forbid();
            }

            if (update == null)
            {
                return BadRequest();
            }

            await botService.ProcessUpdate(update);
            return Ok();
        }
    }
}
