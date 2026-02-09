# Security & Anti-Forgery

When using RazorWire Turbo Streams to replace or update parts of a page that contain forms, the original Anti-Forgery token hidden input may be lost. To prevent `400 Bad Request` errors on subsequent form submissions, ensure the token is included in your updated HTML.

## Recommended: Use the `<form>` TagHelper

If your partial view contains the entire `<form>` element, the ASP.NET Core TagHelper automatically injects the hidden anti-forgery token:

```cshtml
<!-- _MyForm.cshtml -->
<form asp-action="Submit" method="post" id="my-form">
    <input type="text" name="data" />
    <button type="submit">Submit</button>
</form>
```

This is the cleanest approach. Just ensure the entire form is what gets replaced by your Turbo Stream action.

## Fallback: Explicit Token Helper

If your partial only contains form *fields* (not the `<form>` tag itself), use `@Html.AntiForgeryToken()` explicitly:

```cshtml
<!-- _FormFields.cshtml -->
<div id="form-fields">
    @Html.AntiForgeryToken()
    <input type="text" name="data" />
    <button type="submit">Submit</button>
</div>
```

## Summary

| Scenario | Solution |
|----------|----------|
| Partial contains `<form>` | Use `asp-action` TagHelper (automatic) |
| Partial contains only fields | Use `@Html.AntiForgeryToken()` explicitly |

Both methods provide the same security protection.
