using Chess.Api.Dtos;
using Chess.Application.Analytics.Contracts;
using Chess.Application.Services;
using Chess.Application.Sync.Contracts;
using Chess.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Chess.Api.Controllers;

[ApiController]
[Route("api/v1/players")]
public sealed class PlayersController(PlayerService svc, ISyncScheduler sync) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PlayerDto>> Register([FromBody] RegisterPlayerRequest req, CancellationToken ct)
    {
        var player = await svc.RegisterAsync(req.Username, ct);
        await sync.EnqueueAsync(player.Id, SyncMode.Backfill, ct);
        return CreatedAtAction(nameof(Get), new { id = player.Id }, PlayerDto.From(player));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlayerDto>> Get(Guid id, CancellationToken ct)
    {
        var player = await svc.GetByIdAsync(id, ct);
        return Ok(PlayerDto.From(player));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlayerDto>>> ListTracked(CancellationToken ct)
    {
        var players = await svc.ListTrackedAsync(ct);
        return Ok(players.Select(PlayerDto.From).ToList());
    }

    [HttpGet("{id:guid}/analytics/{module}")]
    public async Task<IActionResult> Analytics(
        Guid id,
        string module,
        [FromQuery] AnalyticsFilterQuery filter,
        [FromServices] IAnalyticsRegistry registry,
        CancellationToken ct)
    {
        var result = await registry.Get(module).RunAsync(new AnalyticsContext(id, filter.ToFilter()), ct);
        return Ok(result);
    }
}
