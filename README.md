# AI Product Defect Inspection & Quality Control Assistant

Upload a product photo → **Gemini Vision** detects and classifies defects (English output) → QC reviews/edits the
result → on confirmation the **inventory (passed / failed stock) is updated atomically** and the report is logged.

| Layer    | Tech                                                   |
|----------|--------------------------------------------------------|
| Frontend | React 19 (Vite), axios                                 |
| Backend  | ASP.NET Core Web API (.NET 10)                         |
| Database | SQL Server (LocalDB by default) + EF Core 10 Code First |
| AI       | Google Gemini API (multimodal, structured JSON output) |

## Workflow

```
React upload ─► POST /api/inspections/analyze ─► save to wwwroot/uploads ─► Base64 ─► Gemini (JSON schema)
      ▲                                                                                  │
      │                                       draft InspectionReport (IsUserConfirmed=false)
      │                                                                                  ▼
 dashboard refresh ◄── POST /api/confirm-report ◄── QC reviews / edits AI result (React)
                          (validation + IDbContextTransaction + stock update)
```

## Prerequisites
- .NET SDK 10, Node 20+, SQL Server or LocalDB (`sqllocaldb info` should list `MSSQLLocalDB`)
- `dotnet tool install --global dotnet-ef`
- A Gemini API key from <https://aistudio.google.com/apikey>

## Run

```bash
# 1. Backend
cd backend
dotnet user-secrets set "Gemini:ApiKey" "<YOUR_KEY>"     # never commit the key
dotnet run --urls http://localhost:5080                  # applies migrations + seed data automatically

# 2. Frontend (new terminal)
cd frontend
npm install
npm run dev                                              # http://localhost:5173
```

Change the connection string in `backend/appsettings.json` (`ConnectionStrings:DefaultConnection`) for a full SQL Server.
To manage the schema manually: `dotnet ef migrations add <Name>` / `dotnet ef database update`.

**Offline demo (no API key):** set `Gemini:UseMock=true` (e.g. `$env:Gemini__UseMock="true"`). The mock derives a canned
result from the file name (`good`, `minor`, `major`, `blur`) — it exists only to test the pipeline, not to judge AI quality.

## Database (R1)

`Product` 1─N `InspectionReport` N─1 `Inspector`, and `InspectionReport` N─1 `DefectCategory`, as in the assignment ERD.
Additions and decisions:
- `Product.RowVersion` – optimistic concurrency, so two simultaneous confirmations cannot corrupt stock.
- `CK_Product_Stock` check constraint: `Passed + Failed <= Total`, both ≥ 0.
- FKs use `Restrict` delete so QC history is never silently cascaded away.
- Seed data: 6 defect categories (None/Minor/Major/Critical, English names), 2 inspectors, 3 products.
- `QualityStatus` is `Pass` or `Fail`: minor defects → Pass (with the defect logged); major/critical → Fail.

## API (R3)

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/inspections/analyze` | multipart: `image`, `productId`, `inspectorId` → AI draft report |
| POST | `/api/confirm-report` | JSON `{reportId, defectCategoryId, qualityStatus, defectDescription, quantity}` → confirm + update stock |
| GET | `/api/inspections?status=&productId=&confirmed=&take=` | history |
| GET | `/api/inspections/{id}` | one report |
| DELETE | `/api/inspections/{id}` | discard an unconfirmed draft (+ image file) |
| GET | `/api/products`, POST `/api/products` | products |
| GET | `/api/inspectors`, `/api/defect-categories` | lookups |
| GET | `/api/dashboard/summary` | totals, pass rate, defects by category |

Images are stored in `backend/wwwroot/uploads/<guid>.<ext>` and served statically at `/uploads/...`.
Uploads are limited to 10 MB, JPG/PNG/WebP, validated by extension **and** magic bytes. The `IFormFile` is read once into
memory (position 0), and the same bytes are used for the Base64 payload and the disk copy.

Error codes: `415` bad/corrupt file, `413` too large, `404` unknown product/inspector/report, `422` image blurry/unclear
(`code: IMAGE_UNCLEAR`, nothing saved), `429`/`502` Gemini quota or outage, `400` validation, `409` already confirmed / concurrency.

## AI (R4)

`GeminiVisionService` sends an English prompt + Base64 image to `generateContent` with `responseMimeType=application/json`
and a `responseSchema` (`isImageClear, unclearReason, qualityStatus, defectCategoryId, description, confidence`).
- Retries with exponential backoff on 429/5xx and network errors; 401/403/400 fail fast with a clear message.
- Blurry/dark/irrelevant images (`isImageClear=false` or confidence < 0.4) → `422`, no draft is stored.
- The result is normalised server-side: unknown category id → "No defect"; Major/Critical forces `Fail`.

## Validation & transaction (R5)

`/api/confirm-report` runs inside `IDbContextTransaction`: loads the draft + product, rejects already-confirmed reports (409),
rejects `Major/Critical` marked `Pass` and "No defect" marked `Fail`, requires a description on `Fail`, ensures
`Passed + Failed + quantity ≤ TotalStock`, then updates the report and the product counters and commits — or rolls back entirely.
Real-time feedback: toast notifications on every step, dashboard polls every 5 s and refreshes immediately after a confirmation.

## Test scenarios (R6)

`powershell tools/make-samples.ps1` regenerates `samples/`: `sample_good.png`, `sample_minor.png`, `sample_major.png`,
`sample_blurry.png` (unclear), `sample_faulty.jpg` (not an image). **These are synthetic drawings** for pipeline testing;
for a meaningful Gemini evaluation replace them with real product photos.

| Image | Expected |
|---|---|
| good | Pass, "No defect" |
| minor | Pass, scratch/stain (Minor) |
| major | Fail, dent/crack (Major) |
| blurry | `422 IMAGE_UNCLEAR` |
| faulty | `415` invalid image |

Quick check with curl:
```bash
curl -F image=@samples/sample_major.png -F productId=1 -F inspectorId=1 http://localhost:5080/api/inspections/analyze
curl -H "Content-Type: application/json" -d '{"reportId":1,"defectCategoryId":4,"qualityStatus":"Fail","defectDescription":"Dent","quantity":1}' http://localhost:5080/api/confirm-report
```

## Login / logout
The UI opens on a **Sign in** screen where the QC picks their seeded Inspector account; the session is kept in
`localStorage` and **Log out** (top right) clears it. The signed-in inspector is attached to every upload. This is a
lightweight identity selector for the demo - the API itself has no authentication.

## Status of verification
Verified end to end against **live Gemini** (`gemini-3.5-flash`, `thinkingLevel=low`, ~3-5 s per image) and LocalDB:

| Scenario | Result |
|---|---|
| good / minor / major PNG | `200`, correct category, Pass / Pass / Fail, English description |
| blurry PNG | `422 IMAGE_UNCLEAR`, nothing stored |
| faulty JPG (not an image) | `415` |
| confirm a Major defect as Pass | `400` |
| confirm the same report twice | `409` |
| quantity over total stock | `400` |
| valid confirm | `200`, stock counters updated |

Notes:
- `gemini-2.5-flash` is no longer available to new API keys (404); the model is configurable via `Gemini:Model`.
- Gemini 3.x "thinks" by default, which made one call take 60-90 s. `thinkingLevel=low` fixes that. `minimal` was faster but
  garbled the output text (seen with Vietnamese diacritics), so do not use it.
- Free-tier keys can return `429` (quota) or `503` (high demand). The API retries with backoff, then reports a clear `429/502/504`.
- Keep the API key in user-secrets (`dotnet user-secrets set "Gemini:ApiKey" ...`), not in `appsettings.json`.
