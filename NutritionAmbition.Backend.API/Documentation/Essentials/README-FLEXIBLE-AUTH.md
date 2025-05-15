# Flexible Authorization System for Nutrition Ambition

This document explains how to use the flexible authorization system in the Nutrition Ambition backend, which supports both Firebase-authenticated users and anonymous users.

## Overview

The flexible authorization system consists of:

1. `AnonymousAuthMiddleware` - Processes anonymous user requests
2. `FlexibleAuthorizeAttribute` - Authorization attribute for controllers/actions
3. `HttpContextExtensions.GetAccountFromContextAsync()` - Extension method for getting the account

This system allows users to start using the application anonymously and later transition to a Firebase-authenticated account if desired.

## How It Works

### Anonymous Authentication Flow

1. Client includes `"isAnonymousUser": true` in the request body.
2. If `"accountId"` is also provided, the middleware tries to find that account.
3. If no `accountId` or an invalid one is provided, a new anonymous account is created.
4. The account is stored in `HttpContext.Items["Account"]`.

### Using FlexibleAuthorizeAttribute

Apply this attribute to controllers or actions that should be accessible to both Firebase-authenticated and anonymous users:

```csharp
[ApiController]
[Route("api/[controller]")]
[FlexibleAuthorize]  // Allows both Firebase-authenticated and anonymous users
public class MyController : ControllerBase
{
    // ...
}
```

### Getting the Account in Controllers

Use the extension method to get the current account (whether Firebase-authenticated or anonymous):

```csharp
// Get account using the extension method
var account = await HttpContext.GetAccountFromContextAsync(_accountsService, _logger);
if (account == null)
{
    return Unauthorized();
}

// Now you can use account.Id for operations
```

### Client Implementation

For anonymous users, the client should:

1. **First request:** Send a request with `{ "isAnonymousUser": true }` to any endpoint, which will create an anonymous account and return the account ID.
2. **Subsequent requests:** Include `{ "isAnonymousUser": true, "accountId": "the-id-from-step-1" }` in all requests.
3. Store the anonymous account ID in local storage for persistence between sessions.

All request models that inherit from the base `Request` class automatically include these properties.

## Example Implementation

See `ExampleController.cs` for a demonstration of how to use the flexible authorization system. It includes:

- An endpoint that works with both authentication types
- An example of handling anonymous requests
- Distinguishing between anonymous and authenticated users

## Best Practices

1. Always use `HttpContext.GetAccountFromContextAsync()` to get the current account.
2. Check if the account is null before proceeding.
3. For operations that must be restricted to authenticated users only, use the standard `[Authorize]` attribute.
4. Add appropriate logging to track anonymous vs. authenticated usage patterns.

## Security Considerations

- Anonymous accounts have access only to their own data, just like authenticated accounts.
- Anonymous accounts have no password, so the `accountId` should be treated as a secret.
- Consider implementing a mechanism to migrate data from anonymous accounts to authenticated accounts. 