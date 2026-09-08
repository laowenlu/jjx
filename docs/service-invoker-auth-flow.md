# ServiceInvoker authentication + authorization flow (StarterKit)

This document describes the StarterKit auth architecture used by manager-method RPC calls.

## Key terms

- **ServiceInvoker**: the single API surface that routes frontend calls to backend manager methods.
- **Auth token manager**: `IAuthTokenManager`, implemented by `AuthManager`, owns token refresh and request authentication for ServiceInvoker.
- **Authenticator engine**: provider-neutral token validation and common claim extraction.
- **Auth accessor**: provider-specific auth integration. Local development and Supabase each provide token creation/refresh/validation details through `IAuthAccessor`.
- **AuthZ**: method-level authorization through `[RequireAuthenticated]` and `[RequireRole(...)]`.

## Request path

Normal manager call:

`POST /api/invoke` → `ServiceInvokerEndpoints` → `ServiceMethodInvoker.InvokeAsync`

Streaming manager call:

`POST /api/stream` → `ServiceInvokerEndpoints` → `ServiceMethodInvoker.InvokeStreamingAsync`

## Token transport

The frontend sends tokens in the ServiceInvoker request body:

- `AccessToken`
- `RefreshToken`

The backend returns tokens in the ServiceInvoker response envelope:

- `AccessToken`
- `RefreshToken`
- `Session`
- `Result`

Request/response bodies containing tokens must not be logged or shown in UI.

## Backend flow

For each ServiceInvoker request:

1. `ServiceMethodInvoker` resolves the target manager method.
2. `ServiceMethodInvoker` asks `IAuthTokenManager.EnsureAuthenticatedRequest(...)` to coordinate token refresh and request authentication.
3. `AuthManager` validates/refreshes the request-body token pair.
4. If an access token is available after refresh, `AuthManager` writes it to the current request as an internal `Authorization: Bearer ...` header.
5. `AuthManager` asks the active `IAuthAccessor` for provider-specific `TokenValidationParameters`.
6. `AuthenticatorEngine` validates the bearer token, builds `HttpContext.User`, and populates `UserContextService` with user id, email, and roles.
7. `AttributeMethodAuthorizer` enforces method attributes.
8. The manager method is invoked.
9. The result is wrapped in `ServiceInvocationResponseEnvelopeDto`.

## Session envelope behavior

If a manager returns `SessionDto`, ServiceInvoker uses that returned value as `envelope.Session`.

Otherwise, ServiceInvoker falls back to a basic session derived from `HttpContext.User`.

This lets app-specific session fields flow through session-oriented manager methods without being overwritten by generic JWT claim data.

## Local development provider

`LocalAuthAccessor` owns local-provider behavior:

- local JWT issuer/audience/signing key constants
- local JWT creation
- local refresh behavior
- local access-token validation parameters

Local refresh validates the old access token without lifetime validation so an expired token can still identify the user while exchanging the refresh token.

## Supabase provider

`AuthAccessor` owns Supabase-provider behavior:

- Supabase login/sign-up/refresh calls
- normalization of top-level vs nested `Session` token shapes
- Supabase issuer and audience validation parameters
- JWKS fetch/cache from:

`https://{SupabaseId}.supabase.co/auth/v1/.well-known/jwks.json`

JWKS keys are cached for 15 minutes.

## Authorization

Managers mark protected methods with:

- `[RequireAuthenticated]`
- `[RequireRole(...)]`

Roles are read from Supabase/local JWT `app_metadata.roles` first. If that claim is absent, role claims are used as a fallback.

Malformed `app_metadata` role JSON fails closed rather than being silently ignored.

## Frontend protected routes

`ProtectedRoute` owns protected-route session hydration:

- no tokens → redirect to `/unauthorized`
- tokens but no session → call `AuthManager.GetSession({})`
- restore timeout → show retry/sign-out UI
- restored session → render the protected route

The StarterKit default index route is public. Projects add protected routes only when they need auth-gated views.
