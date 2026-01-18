In @.github/workflows/build.yml:
- Around line 28-32: Move the "Install Aspire Workload" step (the run: dotnet
workload install aspire) to occur before the "Build project" step (the run:
dotnet build --configuration Release) in the GitHub Actions workflow so the
Aspire workload is available during the build; ensure the step names remain
"Install Aspire Workload" and "Build project" and keep the same commands but
reorder them so installation precedes the build.

In `@benchmarks/RunnableBenchmarks/RunnableBenchmarks.csproj`:
- Around line 1-4: The project file RunnableBenchmarks.csproj was updated to
TargetFramework net10.0 but several dependencies (Volo.Abp.AspNetCore,
Volo.Abp.Core, Carter, BenchmarkDotNet) only support up to net9.0/net8.0; either
revert the <TargetFramework> back to net9.0 in RunnableBenchmarks.csproj, or
upgrade those packages to versions that explicitly support net10.0 (update
package references for Volo.Abp.AspNetCore, Volo.Abp.Core, Carter,
BenchmarkDotNet), or add a short compatibility note in the project
README/benchmarks docs confirming forward-compatibility acceptance—pick one
approach and apply consistently.

In `@benchmarks/SimpleApiController/SimpleApiController.csproj`:
- Around line 3-6: The CI workflows are still pinned to older SDKs while the
project (TargetFramework set to net10.0 in SimpleApiController.csproj) and many
others target .NET 10; update the workflow files (build.yml and benchmarks.yml)
to include dotnet-version: 10.0.x (or add a repository-level global.json that
pins the SDK to 10.0.x) so the CI environment can build projects targeting
net10.0; ensure both build.yml and benchmarks.yml are updated and validate the
pipeline runs with the new SDK.

In `@examples/razorwire-mvc/Controllers/ReactivityController.cs`:
- Around line 132-133: Replace the direct ToString() + null-coalescing on the
Referer header with a whitespace check so an empty Referer falls back to "/",
e.g. in ReactivityController where you call
Redirect(Request.Headers["Referer"].ToString() ?? "/"), compute the header
string from Request.Headers["Referer"] and use string.IsNullOrWhiteSpace(header)
to decide whether to pass header or "/" into Redirect; update the Redirect call
accordingly to avoid Redirect("") when the header is present but empty.
- Around line 36-37: Add antiforgery validation to state-changing POST actions
by decorating the RegisterUser, PublishMessage, and IncrementCounter controller
actions with the [ValidateAntiForgeryToken] attribute; update the corresponding
Razor forms (_UserRegistration.cshtml, _MessageForm.cshtml,
Counter/Default.cshtml) to emit tokens via `@Html.AntiForgeryToken`(); ensure any
client-side AJAX POSTs include the antiforgery token header or form field so the
validation on methods RegisterUser, PublishMessage, and IncrementCounter
succeeds.

In `@examples/razorwire-mvc/RazorWireWebExample.csproj`:
- Around line 1-13: Move the <PropertyGroup> block so it appears before the
<ItemGroup> block in the RazorWireWebExample.csproj to follow project file
ordering conventions; locate the <PropertyGroup> containing
TargetFramework/Nullable/AddRazorSupportForMvc/ImplicitUsings and the
<ItemGroup> that contains the ProjectReference and swap their order so the
PropertyGroup is first for improved readability.

In `@examples/razorwire-mvc/README.md`:
- Around line 36-46: The markdown fenced code block starting with "```bash" in
the README is not surrounded by blank lines; update the README so there is an
empty line immediately before the opening ```bash fence and an empty line
immediately after the closing ``` fence (i.e., add a blank line between the
numbered list item text and the opening fence and another blank line after the
closing fence) to satisfy MD031 conventions.
- Around line 26-30: The nested bullet items under
"Controllers/ReactivityController.cs" in README.md use incorrect indentation;
update the nested list items (the three lines starting with "Rendering the main
view.", "Serving partial "Islands" (Sidebar, UserList).", and "Handling form
POSTs and returning Stream responses.") to use 2-space indentation so they are
properly nested under the parent bullet (MD007 compliance) and keep the final
bullet about "Broadcasting updates via the Stream Hub." aligned with the other
nested entries.
- Around line 7-22: Fix markdown linting by adding blank lines before and after
each H3 heading (the lines containing "### 🏝️ Islands (Turbo Frames)", "### 📡
Real-time Streaming (SSE)", and "### ⚡ Form Enhancement") so there is an empty
line separating headings from surrounding content, and normalize list
indentation by changing the 4-space indents used for bullet items (the indented
lines under each feature, e.g., the lines starting with "*   **Example**" and "*
**Code**") to 2-space indentation so bullets align consistently.

In `@examples/razorwire-mvc/Services/IUserPresenceService.cs`:
- Line 3: The UserPresenceInfo record's LastSeen property uses DateTime which is
ambiguous; change the record definition UserPresenceInfo(string Username,
DateTime LastSeen) to use DateTimeOffset for explicit timezone semantics, then
update all usages (constructors, assignments, tests, and any place currently
using DateTime.UtcNow) to use DateTimeOffset.UtcNow or preserved offsets; also
update any serialization/deserialization or persistence code to handle
DateTimeOffset accordingly to avoid breaking changes.

In `@examples/razorwire-mvc/Services/UserPresenceBackgroundService.cs`:
- Around line 35-54: Pulse() returns removed which is enumerated twice and
individual PublishAsync calls per user can be inefficient; materialize removed
into a list (e.g., var removedList = removed.ToList()) immediately after calling
_presence.Pulse() to avoid multiple enumeration, then build a single
RazorWireBridge.CreateStream() that aggregates per-user Remove(...) actions (or
concatenates multiple stream entries) and call _hub.PublishAsync("reactivity",
aggregatedStream) once to batch notifications; also extract the hardcoded
emptyHtml into a view/constant and use that when creating the emptyStream so the
markup isn’t inline.
- Around line 56-62: The Task.Delay call in
UserPresenceBackgroundService.ExecuteAsync can throw OperationCanceledException
when stoppingToken is canceled but it's outside the try-catch, so move or extend
error handling to catch cancellation and optionally log shutdown: either wrap
the await Task.Delay(_checkInterval, stoppingToken) in a try-catch that catches
OperationCanceledException (or check stoppingToken.IsCancellationRequested
before/after delay) and call _logger.LogInformation/Debug to record graceful
shutdown, or include the Task.Delay inside the existing try block and add a
specific catch (OperationCanceledException) to distinguish cancellation from
other exceptions; reference ExecuteAsync, stoppingToken and _logger when making
the change.

In `@examples/razorwire-mvc/ViewComponents/CounterViewComponent.cs`:
- Around line 7-16: The Count getter and the Invoke method read the shared field
_count without a volatile read, so make those reads use Volatile.Read to ensure
cross-thread visibility: change the Count property to return Volatile.Read(ref
_count) and change the Invoke method to pass Volatile.Read(ref _count) to View
while leaving Increment as Interlocked.Increment(ref _count); reference _count,
Count, Invoke, and Increment when locating the changes.

In `@examples/razorwire-mvc/Views/Navigation/Index.cshtml`:
- Around line 24-31: Update the explanatory paragraph that currently references
id="sidebar" to match the actual island id used in the markup
(id="permanent-island"); find the text node containing the string id="sidebar"
in the Views/Navigation/Index.cshtml content and replace it with
id="permanent-island" (alternatively, change the island's HTML id to "sidebar"
if you prefer the original text), ensuring the narrative and the island id
(id="permanent-island") stay consistent.

In `@examples/razorwire-mvc/Views/Reactivity/_MessageForm.cshtml`:
- Around line 3-5: The form currently includes a hidden input named "username"
that the PublishMessage action reads via the username parameter; remove that
input and stop accepting username from the form so the server only derives
identity from Request.Cookies["razorwire-username"]. Specifically, delete the
<input type="hidden" name="username" ...> from the view
(Reactivity/_MessageForm.cshtml) and update the PublishMessage action signature
to remove the username form parameter so it computes effectiveUsername solely
from Request.Cookies["razorwire-username"] (keeping the null-coalescing logic or
handling missing cookie as appropriate).

In `@examples/razorwire-mvc/Views/Reactivity/_RegisterForm.cshtml`:
- Around line 1-3: The username input (`#register-username` inside
`#register-form-container`) lacks an accessible label; add one by either inserting
a visible <label for="register-username">Username</label> associated with the
input or adding an aria-label="Username" attribute to the input element (and
ensure required/placeholder semantics remain intact) so screen readers can
identify the field.

In `@examples/razorwire-mvc/Views/Shared/_Layout.cshtml`:
- Line 8: Update the Tailwind browser script tag in _Layout.cshtml that uses src
"https://unpkg.com/@@tailwindcss/browser@4" to include Subresource Integrity and
CORS attributes: compute the sha384 SRI hash (e.g., via the provided curl |
openssl command), then add integrity="sha384-<HASH>" and crossorigin="anonymous"
to the <script> tag so it matches the integrity usage pattern used by the
Turbo.js script.

In `@examples/razorwire-mvc/Views/Shared/Components/UserList/_UserItem.cshtml`:
- Around line 3-8: The li element uses Model.Username directly in the id
(id="user-@Model.Username"), which can produce invalid DOM ids; change to use a
sanitized/consistent id property or helper (e.g., Model.DomId,
Model.SanitizedUsername, or a helper like Html.IdFor/UrlEncoder) and update any
Turbo stream targets to use that same sanitized id; locate the id usage in
_UserItem.cshtml and replace Model.Username with the sanitized identifier
throughout the component and any related views or scripts.

In `@examples/razorwire-mvc/Views/Shared/Components/UserList/Default.cshtml`:
- Around line 3-22: The view calls Model.Count() inside the user-count span
before any null guard which can throw when Model is null; make the view
null-safe and avoid double-enumeration by materializing Model once (e.g., assign
a local variable like users = Model?.ToList() or similar) at the top of the
template, use users.Count (or users?.Count ?? 0) for the "#user-count" span, and
then iterate over that same users collection in the foreach/RenderPartialAsync
block and use the existing null/empty check against users (instead of Model) for
the empty-state branch.

In `@examples/razorwire-mvc/wwwroot/css/site.css`:
- Around line 2-12: The CSS currently targets [disabled] and
[data-rw-requires-stream][disabled] but misses elements disabled via ARIA;
update the selectors to also cover [aria-disabled="true"] and
[data-rw-requires-stream][aria-disabled="true"] (and their descendant rules like
[disabled] * / [aria-disabled="true"] *) so non-form controls styled via ARIA
receive the same pointer-events, opacity and cursor treatments as the existing
[disabled] rules.
- Around line 20-30: The CSS rules for .card-premium, .input-premium, and
.btn-premium currently use Tailwind `@apply` directives which will be ignored when
served directly from wwwroot; either replace the `@apply` usage with equivalent
plain CSS declarations (e.g., explicit background-color, border, border-radius,
padding, box-shadow, transition, etc. for
.card-premium/.input-premium/.btn-premium) or add a Tailwind/PostCSS build step
(including tailwind.config.* and postcss.config.* and wiring it into the project
build) so the `@apply` directives are compiled—pick one approach and update the
classes or project build accordingly.

In `@examples/razorwire-mvc/wwwroot/js/site.js`:
- Around line 12-14: The event listeners for 'razorwire:stream:connecting',
'razorwire:stream:connected', and 'razorwire:stream:disconnected' assume
e.detail and e.detail.source exist and will throw if they're undefined; update
the handlers in site.js to use optional chaining (e?.detail?.channel and
e?.detail?.source?.id) and/or add a small helper (e.g., formatStreamLabel or
getStreamId) to safely build the template string so missing detail/source won't
break the console logging. Ensure all three listeners (the arrow callbacks) use
the helper or optional chaining consistently to produce the same fallback label
when values are absent.

In `@ForgeTrust.Runnable.Core.Tests/RunnableStartupTests.cs`:
- Line 5: Add a CollectionDefinition for the "NoParallel" test collection so the
[Collection("NoParallel")] attribute actually disables parallelization: create a
public class (e.g., NoParallelCollection) annotated with
[CollectionDefinition("NoParallel", DisableParallelization = true)] in the test
assembly to enforce serial execution for tests using the "NoParallel"
collection.

In `@ForgeTrust.Runnable.slnx`:
- Around line 14-15: The solution file ForgeTrust.Runnable.slnx contains mixed
path separators in Project Path entries (e.g., Project
Path="Console\\ForgeTrust.Runnable.Console.Tests\\ForgeTrust.Runnable.Console.csproj");
normalize all Project Path attributes to use forward slashes (/) for
cross-platform consistency by replacing backslashes (\\) with slashes in every
Project Path value, verifying entries such as
Console/ForgeTrust.Runnable.Console.Tests/ForgeTrust.Runnable.Console.csproj and
other listed projects are updated and still resolve correctly in the solution
parser.

In `@Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ExportCommand.cs`:
- Around line 16-30: Validate BaseUrl at the start of ExecuteAsync (use
Uri.TryCreate and ensure IsAbsoluteUri with http/https); if invalid throw a
CliFx.Exceptions.CommandException with a clear message (so add "using
CliFx.Exceptions;"). Also address the unused Mode: either remove the Mode
parameter from the ExportEngine constructor call and from ExportEngine API if no
mode behavior is needed, or implement the mode logic inside
ExportEngine.RunAsync (e.g., switch on Mode for "s3" vs "hybrid") and ensure
ExportEngine stores and uses the Mode value; update the ExportCommand
constructor call accordingly.

In `@Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ExportEngine.cs`:
- Around line 95-106: MapRouteToFilePath currently accepts raw routes and can be
abused or produce invalid filenames; update it to first strip query strings and
fragments (anything after '?' or '#'), reject any route containing ".." segments
or empty/invalid path segments, normalize leading/trailing slashes, replace
invalid filename characters
(Path.GetInvalidFileNameChars/Path.GetInvalidPathChars) in segments, then build
the target path and call Path.GetFullPath to resolve it and verify the resulting
path starts with the full _outputPath (reject otherwise). Also update
ExtractLinks and ExtractFrames to enqueue only these normalized, validated
routes (use the same normalization routine), remove duplicates (use a HashSet)
and skip invalid/rejected routes before processing. Ensure all references to
MapRouteToFilePath, ExtractLinks, ExtractFrames and _outputPath are used so path
traversal and invalid filename cases are prevented.

In
`@Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj`:
- Around line 1-17: Move the <PropertyGroup> block so it appears before the two
<ItemGroup> blocks in the project file to match repository conventions;
specifically, relocate the block containing OutputType, TargetFramework
(net10.0), ImplicitUsings and Nullable so it precedes the ProjectReference and
PackageReference ItemGroup entries (including the PackageReference
Include="CliFx" Version="2.3.6") in
ForgeTrust.Runnable.Web.RazorWire.Cli.csproj.

In `@Web/ForgeTrust.Runnable.Web.RazorWire.Cli/README.md`:
- Around line 15-29: The README has missing blank lines around the "###
`export`" heading and the example code fence causing markdownlint MD022/MD031;
add a blank line before the "### `export`" heading and ensure there is a blank
line above and below the triple-backtick example block so the heading and code
fence are separated from surrounding text (update the README content around the
`### export` heading and the example code fence accordingly).

In `@Web/ForgeTrust.Runnable.Web.RazorWire/Component1.razor.css`:
- Line 1: The CSS file has a hidden BOM/invalid leading character before the
selector .my-component causing lint to report an unknown type selector; open
Component1.razor.css, remove the leading invisible character (or re-save the
file as UTF-8 without BOM) so the file begins with ".my-component" and re-run
lint/CI to confirm the error is resolved.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/ExampleJsInterop.cs`:
- Around line 17-20: The Prompt method currently returns ValueTask<string> but
browser prompt() can return null; update the method signature to
ValueTask<string?> Prompt(string message) and call await
module.InvokeAsync<string?>("showPrompt", message) so nullability is preserved
and callers can handle a null result; keep using moduleTask.Value and await as
before but ensure the return type and InvokeAsync generic are changed to
string?.

In
`@Web/ForgeTrust.Runnable.Web.RazorWire/ForgeTrust.Runnable.Web.RazorWire.csproj`:
- Around line 3-9: Remove the excessive blank lines inside the PropertyGroup to
clean up formatting; specifically collapse the empty lines between the elements
(TargetFramework, Nullable, AddRazorSupportForMvc, ImplicitUsings,
StaticWebAssetBasePath) so the PropertyGroup is compact and contiguous, leaving
only single line breaks between each XML element.

In
`@Web/ForgeTrust.Runnable.Web.RazorWire/RazorWireEndpointRouteBuilderExtensions.cs`:
- Around line 50-76: The loop races reader.ReadAsync(...) against
Task.Delay(...) allowing concurrent reads; replace the Task.WhenAny approach by
calling reader.ReadAsync with a timeout-bound CancellationToken so only one
ReadAsync is active: create a CancellationTokenSource linked to
context.RequestAborted, call cts.CancelAfter(20000) and await
reader.ReadAsync(cts.Token) (handle
OperationCanceledException/TaskCanceledException to send the heartbeat and
continue), dispose the cts, and remove the Task.Delay/Task.WhenAny logic to
ensure no concurrent reader.ReadAsync calls.

In
`@Web/ForgeTrust.Runnable.Web.RazorWire/RazorWireServiceCollectionExtensions.cs`:
- Around line 9-16: The current AddRazorWire method registers a concrete
RazorWireOptions instance directly which prevents resolution via
IOptions<RazorWireOptions>; change the registration to use the Options pattern
by calling services.AddOptions().Configure<RazorWireOptions>(...) or
services.Configure<RazorWireOptions>(opts => { /* apply configure action */ })
inside AddRazorWire (use the existing configure Action<RazorWireOptions>? to
populate the options), and if you still need direct injection of the concrete
type register a factory like services.AddSingleton(provider =>
provider.GetRequiredService<IOptions<RazorWireOptions>>().Value) so both
IOptions<RazorWireOptions> and direct RazorWireOptions consumers resolve
correctly (modify the AddRazorWire method and replace the
services.AddSingleton(options) usage).

In `@Web/ForgeTrust.Runnable.Web.RazorWire/README.md`:
- Around line 13-14: Update the README text under the "Form Enhancement"
section: replace the phrase "partial page updates" with the hyphenated form
"partial-page updates" wherever it appears (for example in the sentence starting
"Standard HTML forms are enhanced to perform partial page updates") to correct
grammar.
- Around line 5-124: Add blank lines before and after each Markdown heading to
satisfy MD022; update headings such as "## Core Concepts", "### 🏝️ Islands
(Turbo Frames)", "### 📡 Real-time Streaming (Turbo Streams & SSE)", "### ⚡ Form
Enhancement", "## Getting Started", "### 1. Add the Module", "### 2. Configure
Services (Optional)", "### 3. Use in Controllers", "## API Reference", "###
`RazorWireBridge`", "### `IRazorWireStreamHub`", "### `this.RazorWireStream()`
(Controller Extension)", "## TagHelpers", "### `rw:island`", "### `rw:form`",
"### `rw:scripts`", "## Client-Side Interop (Hybrid Components)", "## Static
Export", and "## Examples" so that there is exactly one blank line above and
below each heading throughout the README.md content.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/Streams/InMemoryRazorWireStreamHub.cs`:
- Around line 39-47: The Unsubscribe method currently removes the writer from
_readerToWriter and the channel's subscribers but doesn't complete the writer,
so any awaiting readers may hang; update the Unsubscribe(string channel,
ChannelReader<string> reader) implementation to call writer.TryComplete() (or
TryComplete(exception) if you want to propagate an error) after successfully
removing it from _readerToWriter and from the _channels subscribers collection
to release pending ReadAsync calls and allow the channel pair to be garbage
collected.
- Around line 11-19: PublishAsync currently awaits subscriber.WriteAsync which
can throw ChannelClosedException and abort broadcasting; change the loop that
iterates over _channels[channel] inside PublishAsync to use
subscriber.TryWrite(message) for non-blocking writes, detect when TryWrite
returns false (or when the channel is completed/closed) and remove that
subscriber from the subscribers collection so closed writers are pruned,
ensuring remaining subscribers still receive the message; keep the dictionary
_channels and the PublishAsync method semantics but replace await WriteAsync
calls with TryWrite checks and removal of closed writers.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/TagHelpers/IslandTagHelper.cs`:
- Around line 58-61: The current logic in IslandTagHelper.cs uses
output.Attributes.SetAttribute("style", ...) which overwrites any existing
style; change it to read the existing style attribute from output.Attributes
(e.g., find attribute with Name == "style"), build a combined style string by
appending $"view-transition-name: {TransitionName};" to the existing value when
present, and then call output.Attributes.SetAttribute("style", combinedValue);
reference TransitionName, output.Attributes and SetAttribute to locate where to
change the behavior.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/TagHelpers/RazorWireFormTagHelper.cs`:
- Around line 15-28: In Process method of RazorWireFormTagHelper, after reading
the helper properties (e.g., Enabled, TargetFrame) and setting the turbo
attributes, remove any custom "rw-*" attributes from the rendered output so they
are not emitted into the DOM; locate the output handling in
RazorWireFormTagHelper.Process and drop attributes whose names start with "rw-"
(e.g., via output.Attributes.RemoveAll(a => a.Name.StartsWith("rw-")) or
equivalent) so the tag helper logic uses those inputs but they are stripped from
the final HTML.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/TagHelpers/RequiresStreamTagHelper.cs`:
- Around line 11-23: The Process method should add accessibility attributes when
marking elements disabled: when RequiresStream is set, in addition to
output.Attributes.SetAttribute("disabled", "disabled"), add
output.Attributes.SetAttribute("aria-disabled", "true"); also ensure you only
emit the native disabled attribute for form controls (e.g., check output.TagName
or context to determine input/button/select/textarea) and for non-form elements
emit aria-disabled="true" and consider making them unfocusable (e.g., set
tabindex="-1") instead of setting the disabled attribute so screen readers and
keyboard behavior are correct.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/TagHelpers/StreamSourceTagHelper.cs`:
- Around line 6-31: In Process of StreamSourceTagHelper validate that Channel is
not null/empty before building src: check string.IsNullOrWhiteSpace(Channel) and
if so throw an ArgumentException (or ArgumentNullException) referencing the
Channel parameter; otherwise construct src using _options.Streams.BasePath and
Channel and set the "src" attribute as before (update the Channel property usage
and the Process method to include this guard so you never produce
"/_rw/streams/" for a missing channel).

In `@Web/ForgeTrust.Runnable.Web.RazorWire/Turbo/PartialViewStreamAction.cs`:
- Around line 62-63: The returned turbo-stream string in PartialViewStreamAction
is interpolating _action without encoding; update the method to HTML-encode
_action (e.g., using HtmlEncoder.Default.Encode) similar to how _target is
encoded (encodedTarget) and use that encodedAction in the returned string so
both attributes are safely encoded to prevent potential XSS.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/Turbo/RazorWireStreamBuilder.cs`:
- Around line 166-168: BuildResult currently passes the mutable field _actions
directly into the RazorWireStreamResult which allows later builder mutations to
affect already-built results; change BuildResult (method BuildResult) to pass an
immutable snapshot of _actions (e.g., create a shallow copy or convert to a
read-only/IReadOnlyList) when constructing RazorWireStreamResult so the result
holds a stable collection independent of further builder modifications.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/Turbo/RazorWireStreamResult.cs`:
- Around line 22-25: The RawHtmlStreamAction constructor stored rawContent
without validation causing WriteAsync to receive null; update
RazorWireStreamResult's constructor (the one that creates new
RawHtmlStreamAction(rawContent)) to coalesce rawContent to an empty string
(e.g., rawContent ?? string.Empty) before passing it into RawHtmlStreamAction,
and apply the same null-coalesce safeguard to the other constructor/assignment
around lines 45–49 that also instantiates RawHtmlStreamAction so
RenderAsync/WriteAsync never receive null.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/Turbo/TurboRequestExtensions.cs`:
- Around line 7-10: Update IsTurboRequest to perform a case-insensitive check of
the Accept header value so MIME matching follows RFC 7231; read
request.Headers["Accept"] safely (fall back to empty string if missing) and
search for "text/vnd.turbo-stream.html" using a case-insensitive comparison
(e.g., IndexOf or string.Contains overload with StringComparison) to determine
membership.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/Turbo/ViewComponentStreamAction.cs`:
- Around line 28-36: The ViewContext creation for componentViewContext is
instantiating a new HtmlHelperOptions which can drop configured options; update
both places where a new HtmlHelperOptions is passed (the ViewContext constructor
that creates componentViewContext and the second similar instantiation) to use
viewContext.HtmlHelperOptions instead so existing settings (e.g., client
validation) are preserved; locate the ViewContext constructor call that assigns
to componentViewContext and replace the new HtmlHelperOptions() argument with
viewContext.HtmlHelperOptions, and do the same for the other matching
ViewContext construction in this class.
- Around line 24-48: In both ViewComponentStreamAction.RenderAsync and
ViewComponentByNameStreamAction.RenderAsync, the _action field is interpolated
into the turbo-stream action attribute without encoding; update those methods to
Html-encode _action (e.g., use HtmlEncoder.Default.Encode(_action)) before
interpolation so the attribute value is safe from injection, keeping _target
encoding as-is; locate the return string construction in each RenderAsync and
replace raw _action usage with the encoded value.

In
`@Web/ForgeTrust.Runnable.Web.RazorWire/wwwroot/razorwire/razorwire.islands.js`:
- Around line 10-48: hydrateIslands currently parses props with JSON.parse
(which can throw) and only marks islands as hydrated after async mountIsland
completes, allowing duplicate scheduling and observers; fix by wrapping
JSON.parse in try/catch and falling back to {} (log parse errors), and mark the
element as scheduled/initialized immediately when you decide a strategy so
subsequent hydrateIslands calls skip it (use initializedElements.add(root) and
set data-rw-hydrated or a temporary attribute when scheduling
observers/timeouts/requestIdleCallback), and ensure mountIsland still finalizes
hydration (set data-rw-hydrated and handle mount errors) while any scheduling
callbacks first check initializedElements before mounting; reference
functions/vars: hydrateIslands, mountIsland, setupIntersectionObserver,
initializedElements, and the data-rw-props parsing.

In `@Web/ForgeTrust.Runnable.Web.RazorWire/wwwroot/razorwire/razorwire.js`:
- Around line 276-285: getChannelName currently returns only the last pathname
segment which causes collisions for URLs that differ by query or longer paths;
update getChannelName to incorporate the URL.search (and optionally URL.hash)
into the returned channel name, sanitize the combined string (e.g., replace
non-alphanumeric characters with underscores and trim length to a safe limit)
and still handle invalid URLs by returning null; locate the getChannelName
function and change the return to a sanitized combination of segments.pop() +
'_' + url.search (and/or url.hash) so channel names remain unique for different
query params.
- Around line 234-258: The current updateSourceState in razorwire.js lets a
missing source with state 'disconnected' continue and re-add channel/body state;
change the early-return logic so that if the source is not found
(this.sources.get(src) is falsy) the function returns immediately and does not
compute channel, set this.channelStates, call this.updateBodyAttribute,
this.syncDependentElements, or dispatch to elements; updateSourceState should
only proceed when a tracked source exists (and preserve existing behavior for
real connects on tracked sources).

In `@Web/ForgeTrust.Runnable.Web.Tests/ForgeTrust.Runnable.Web.Tests.csproj`:
- Line 4: The repo projects target net10.0 (see TargetFramework value net10.0)
but CI is pinned to .NET 9.x; update the workflows to include .NET 10 so builds
restore correctly: in the build workflow change the single `dotnet-version:
9.0.x` entry to `10.0.x` (or a list that includes `10.0.x`), and in the
benchmarks workflow add `10.0.x` to the multi-version `dotnet-version` list so
the pipeline runs against .NET 10 as well.

In `@Web/ForgeTrust.Runnable.Web.Tests/WebStartupTests.cs`:
- Around line 241-244: Remove the unused static field _count from the
TestWebModule class to eliminate dead code; locate the TestWebModule class
(implements IRunnableWebModule) and delete the declaration "private static int
_count;" so only relevant members like the MvcLevel property remain.
- Around line 84-142: The tests mutate ASPNETCORE_ENVIRONMENT; instead inject a
test environment provider into StartupContext instead of changing global state.
Replace creating StartupContext(...) that relies on environment variables with a
constructor call that passes an EnvironmentProvider (e.g. use
TestEnvironmentProvider with Environments.Development or Environments.Production
and appropriate isDevelopment flag) so StartupContext captures the desired
environment without Environment.SetEnvironmentVariable; see StartupContext,
TestEnvironmentProvider and DefaultEnvironmentProvider for reference and mirror
the pattern used in DefaultEnvironmentProviderTests.

In `@Web/ForgeTrust.Runnable.Web/StaticFilesOptions.cs`:
- Around line 3-17: Replace the static readonly field Default with a static
property to match the CorsOptions pattern: change the declaration "public static
readonly StaticFilesOptions Default = new();" to a static get-only property e.g.
"public static StaticFilesOptions Default { get; } = new();". Keep the rest of
the record (EnableStaticFiles, EnableStaticWebAssets) unchanged; this yields the
safer default-access pattern used by CorsOptions (and consider applying the same
change to MvcOptions).