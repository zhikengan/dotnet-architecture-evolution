# ADR-0011: S3-compatible storage with presigned URLs

**Status:** Accepted (Tier 4)

## Context

Product images (and any other binary content) belong in object storage, not in Postgres. Two integration shapes exist for upload:

1. **API-mediated** — client POSTs bytes to the API; API streams them to S3. Simple but doubles bandwidth on the API host and lets a single big upload starve other requests.
2. **Presigned URL** — client requests a short-lived signed URL from the API; PUTs bytes directly to S3; tells the API "I'm done". API only touches metadata, never the bytes.

Production marketplaces use presigned URLs. The pattern is also where most teams hit the dev/prod gap — MinIO in dev/CI, AWS S3 in prod, slightly different config.

## Decision

**`IFileStorage` abstraction with an S3-compatible implementation (AWSSDK.S3) and a local-filesystem fallback for unit tests. Presigned PUT URLs only.**

- `IFileStorage` (BuildingBlocks/Infrastructure/Storage) has three methods: `GeneratePresignedUploadUrlAsync(key, contentType, ttl)`, `GeneratePublicUrl(key)`, `ExistsAsync(key)`.
- `S3FileStorage` uses `AWSSDK.S3` against either MinIO (`ForcePathStyle = true`, `UseHttp = true`) or AWS S3 (default settings) — switched purely by `StorageOptions.Endpoint`. Presigned URLs honor `Protocol.HTTP` so MinIO `http://` URLs aren't silently rewritten to `https://`.
- `LocalFileStorage` writes to a temp dir, returns `file://` URIs. Unit tests use it; the e2e suite uses `Testcontainers.Minio` for real bucket round-trip.
- **`PublicEndpoint` vs `Endpoint`**: the SDK calls MinIO at `http://minio:9000` from inside the docker network; browsers call `http://localhost:9000`. The two are different URLs for the same bucket. `StorageOptions.PublicEndpoint` carries the browser-facing host; `Endpoint` carries the SDK-side host. Production with a CDN sets `PublicEndpoint` to the CDN.

**Use case shape** — seller uploads a product image:

1. `POST /api/seller/products/{id}/image-upload-url` body `{ contentType }` → server returns `{ uploadUrl, publicUrl, key, expiresAt }`. Key is `{tenantId}/{productId}/{guid}` so cross-tenant collisions are impossible.
2. Client `PUT`s bytes directly to `uploadUrl`. The API host never sees them.
3. `POST /api/seller/products/{id}/image` body `{ key }` → server checks `IFileStorage.ExistsAsync(key)` to defend against a malicious caller claiming a key they never uploaded, then sets `Product.ImageKey`.
4. Buyer's `BuyerProductDto.ImageUrl` is populated from `IFileStorage.GeneratePublicUrl(product.ImageKey)`.

## Consequences

**Positive.**
- API host stays out of the bandwidth path. Image uploads scale with the object store, not the API process.
- The presigned URL has a short TTL (15 min) so a leaked link self-expires.
- The same SDK works in dev (MinIO), CI (Testcontainers.Minio), and prod (AWS S3) — just change the endpoint. Tier 5's distributed setup can swap in CloudFront/CDN without touching application code.
- The "confirm" step defends against malicious clients claiming arbitrary keys.

**Negative.**
- Two-step upload is more complex for clients than a single API call. Documented in the new-module how-to.
- `PublicEndpoint` must be configured correctly per environment — wrong value = images that fail to load in the browser even though uploads succeed. Documented in `appsettings.json` comments and `docker-compose.yml`.
- AWSSDK.S3 is a heavy dependency in BuildingBlocks. Acceptable because the alternative (a minimal hand-rolled S3 signing implementation) is more code and worse-tested than the SDK.

## Alternatives considered

- **API-mediated streaming.** Cheap to implement; expensive to operate. Postpone forever — there's no value in writing it just to throw it away.
- **Hand-rolled S3 signing.** Fewer dependencies but rolling your own crypto for an SDK that AWS gives away for free is a poor trade.
- **Azure Blob / Google Cloud Storage SDK.** Same shape, different SDK. `IFileStorage` is the seam — adding `AzureBlobFileStorage` later is one class.
