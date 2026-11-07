using System.Diagnostics;
using Awards.Api.Contracts;
using Awards.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Awards.Api.Controllers;

[ApiController]
[Route("awards")]
public sealed class AwardsController(PointsService pointsService) : ControllerBase
{
    /// <summary>
    /// Creates an award, or returns the existing one on an identical retry. There is no
    /// Location header on 201 because the API has no endpoint for reading a single award.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<AwardResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<AwardResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public ActionResult<AwardResponse> Create([FromBody] CreateAwardRequest request)
    {
        var award = request.ToAward();

        return pointsService.CreateAward(award) switch
        {
            AwardOutcome.Created => StatusCode(StatusCodes.Status201Created, AwardResponse.From(award)),
            AwardOutcome.Replayed => Ok(AwardResponse.From(award)),
            AwardOutcome.Conflict => Problem(
                title: "Award conflict",
                detail: "An award with this sourceId and externalTransactionId already exists with different values.",
                statusCode: StatusCodes.Status409Conflict),
            AwardOutcome.WalletNotFound => Problem(
                title: "Wallet not found",
                detail: $"Wallet '{award.WalletId}' does not exist.",
                statusCode: StatusCodes.Status404NotFound),
            var outcome => throw new UnreachableException($"Unhandled award outcome: {outcome}"),
        };
    }
}
