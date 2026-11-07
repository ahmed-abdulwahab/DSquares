# Awards API

A .NET 10 Web API that awards points to wallets. It handles retries safely, detects conflicting
requests, and keeps balances correct when requests arrive at the same time.

I completed this task in approximately 100 minutes with the help of AI.

## Run

```bash
dotnet run --project src/Awards.Api --launch-profile http
```

Then open Swagger at <http://localhost:5117/swagger>.

Data is stored in memory and resets when the app restarts. The starting balances are W100 with
100 points and W200 with 50 points.

## Test

```bash
dotnet test
```

## Endpoints

### `POST /awards`

```json
{ "sourceId": "S1", "externalTransactionId": "T1", "walletId": "W100", "points": 25 }
```

`sourceId` and `externalTransactionId` together identify an award.

| Status | Meaning |
|---|---|
| 201 | Award created and wallet credited |
| 200 | Identical retry, nothing changed |
| 400 | Invalid JSON or invalid fields |
| 404 | Wallet does not exist |
| 409 | Same identity with a different wallet or points |
| 415 | `Content-Type` is not JSON |

Validation runs first, then the existing award check, then the wallet check. This means a
conflicting retry that names an unknown wallet returns 409, not 404.

### `GET /wallets/{walletId}`

Returns `{ "walletId": "W100", "balance": 100 }`, a 400 for an invalid ID, or a 404 for an unknown
wallet.

### Errors

All errors use the standard `ProblemDetails` JSON format.

## Design

- **Structure:** controllers handle HTTP, `PointsService` holds the business rules, and
  `InMemoryStore` holds the data.
- **Validation:** standard data annotations on the request model.
- **Concurrency:** `InMemoryStore` is a singleton with one lock, so checking for an award, saving it
  and crediting the wallet happen as a single step. This works for a single process, which is the
  scope of this task.
