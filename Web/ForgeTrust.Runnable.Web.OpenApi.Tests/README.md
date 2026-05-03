# ForgeTrust.Runnable.Web.OpenApi.Tests

This test project verifies the public behavior of `ForgeTrust.Runnable.Web.OpenApi`.

## Coverage

- `RunnableWebOpenApiModule.ConfigureServices` registers OpenAPI document generation and endpoint API explorer services.
- `RunnableWebOpenApiModule.ConfigureEndpoints` maps the conventional `/openapi/{documentName}.json` endpoint.
- Hosted integration coverage confirms generated OpenAPI documents use the `StartupContext.ApplicationName` title.
- Hosted integration coverage confirms the default document and operation transformers remove framework-owned `ForgeTrust.Runnable.Web` tags while preserving consumer tags.

The tests exercise the module through its public `IRunnableWebModule` entry points and a real `WebStartup<RunnableWebOpenApiModule>` host. This keeps the contract focused on observable package behavior rather than private OpenAPI option internals.
