# Weather Image Generator (Azure Functions)

Implements the 2025 assignment requirements:

- `POST /api/jobs/start` starts a job and enqueues it (`image-start`)
- `image-start` fan-outs into 50 station tasks (`image-process`)
- `image-process` fetches a public background image, writes station + weather info on it, and stores the result in Blob Storage
- `GET /api/jobs/{jobId}/status` reads `jobs/{jobId}/status.json`
- `GET /api/jobs/{jobId}/images` lists image URLs (SAS by default, or direct URLs when `PUBLIC_BLOB_ACCESS=true`)

## Configuration (App Settings)

- `BUIENRADAR_API` (default: Buienradar feed)
- `PEXELS_API_KEY` (required to fetch images)
- `OUTPUT_BLOB_CONTAINER` (default: `weather-images`)
- `CACHE_BLOB_CONTAINER` (default: `background-cache`)
- `PUBLIC_BLOB_ACCESS` (`true` to return direct blob URLs, `false` to return SAS URLs when possible)

## Local run

1. Put your Pexels key in `local.settings.json`
2. Start Azurite (or use a real storage account)
3. Run:
   - `func start` (or via VS Code)

Use `api.http` as the API documentation / test client.
