using System.ComponentModel.DataAnnotations;
using Awards.Api.Contracts;
using Awards.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Awards.Api.Controllers;

[ApiController]
[Route("wallets")]
public sealed class WalletsController(PointsService pointsService) : ControllerBase
{
    [HttpGet("{walletId}")]
    [ProducesResponseType<WalletResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public ActionResult<WalletResponse> Get(
        [RegularExpression(IdentifierRules.Pattern, ErrorMessage = IdentifierRules.ErrorMessage)] string walletId)
    {
        return pointsService.GetBalance(walletId) is { } balance
            ? Ok(new WalletResponse(walletId, balance))
            : Problem(
                title: "Wallet not found",
                detail: $"Wallet '{walletId}' does not exist.",
                statusCode: StatusCodes.Status404NotFound);
    }
}
