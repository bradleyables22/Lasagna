# Lasagna

Lasagna is a .NET command-line tool for saving and reusing files in .NET projects.

It is intended for project building blocks that are useful across projects but do not need a complete project template. Save a Razor component, service, configuration file, or related group of files once, then pull it into another project whenever you need it.

Files copied into a project remain ordinary project files. Lasagna does not keep them linked to the stored copy, so you can change them freely after loading them.

## How it works

Lasagna has three types of reusable content:

- **Items** are named collections of files and folders.
- **Bundles** are manifest files that reference multiple items and specify where each item should be placed.
- **Kits** are snapshots of a directory's contents that can be copied into another project.

Items, bundle manifests, and kits are stored in the user's local application-data directory under the Lasagna storage folder. A bundle references items by name rather than duplicating their files. A kit stores the relative contents of a captured directory.

Kits skip generated or machine-specific directories by default, including `.git`, `.vs`, `bin`, `obj`, `TestResults`, and `.idea`.

When files are loaded, Lasagna works from the current working directory. Relative paths in bundle manifests are resolved against that directory.

Command names, command options, item names, bundle names, and kit names are case-insensitive. File paths follow the rules of the operating system, while namespace text keeps C# and Razor's normal casing semantics.

## Quick start

Create an item from a file:

```text
lasagna item create card Components/Card.razor
```

List everything in the Lasagna pantry:

```text
lasagna list
```

Load the item into the current project:

```text
lasagna load card
```

The target namespace is detected from the nearest project file when possible. You can provide it explicitly:

```text
lasagna load card --namespace MyApp.Pages
```

Capture a complete project or directory as a kit:

```text
lasagna kit create orders-api .
```

## Creating items

Use `item create` with a name and one or more files or directories:

```text
lasagna item create logging Logging/LoggingService.cs
lasagna item create admin-ui Components/Admin Services/AdminService.cs
lasagna item create shared-components Components/Shared
```

Selecting a directory preserves its folder structure inside the stored item.

When a Razor file is selected, Lasagna looks for related files:

- `Component.razor`
- `Component.razor.cs`
- `Component.razor.css`
- `Component.razor.js`

When related files are found, Lasagna lists them with numbers and includes them by default. Enter the numbers you want to exclude, separated by commas, or press Enter to include everything. In non-interactive terminals, discovered companions are included automatically.

To skip companion discovery entirely and create the item from only the explicitly selected files, use:

```text
lasagna item create card Components/Card.razor --no-companions
```

Lasagna automatically detects the source namespace from the nearest project and the selected source files. You can override that detection for unusual project layouts:

```text
lasagna item create card Components/Card.razor --source-namespace MyApp.Components
```

## Loading items

Use `load` to copy an item, bundle, or kit into the current working directory:

```text
lasagna load logging
lasagna load admin-ui --overwrite
lasagna load card --destination Components
```

Loaded files go into the current working directory by default. Use `--destination` to place them under a relative folder. Existing files are protected by default; use `--overwrite` when replacement is intentional.

For namespace-aware files, Lasagna can rewrite the stored namespace to the target project's namespace. Use `--namespace` to override automatic project detection:

```text
lasagna load card --namespace MyApp.Features
```

To copy namespaces exactly as stored, use:

```text
lasagna load card --no-namespace-rewrite
```

Namespace rewriting applies to C# and Razor-based source files. Other file types are copied without modification.

## Kits

Use a kit when you want to save the contents of a project or any directory as one reusable source set. The command requires an explicit directory so a whole-folder capture is intentional:

```text
lasagna kit create orders-api path/to/Orders.Api
```

The directory contents are captured without treating the directory as a buildable project. Project and solution files such as `.csproj`, `.fsproj`, `.vbproj`, `.sln`, `.slnx`, and `.slnf` are skipped. Generated directories such as `.git`, `.vs`, `bin`, `obj`, `TestResults`, and `.idea` are skipped automatically.

Update a kit by recapturing a directory:

```text
lasagna kit update orders-api path/to/Orders.Api
```

List kits or inspect the files inside one:

```text
lasagna kit list
lasagna kit list orders-api
```

Delete a kit:

```text
lasagna kit delete orders-api
```

Load a kit into another project. Namespace rewriting is enabled by default:

```text
lasagna load orders-api --destination src/Orders
lasagna load orders-api --namespace MyCompany.Orders
lasagna load orders-api --no-namespace-rewrite
```

During every load, Lasagna aligns the stored content with the target project's namespace. It discovers a kit's
namespace hierarchy when needed, rewrites C# and Razor namespace references recursively, validates every rewrite
before writing, and reports the number of files and references changed:

```text
lasagna load orders-api --destination src/Orders
```

The legacy `--migrate` option is still accepted as a compatibility alias, but it is no longer required. Use
`--no-namespace-rewrite` when the stored namespaces must be copied exactly.

The target namespace is detected from the nearest project file when possible. C# and Razor source files are rewritten recursively; unsupported file types are copied unchanged.

From the browser, open `/kits/<name>` to add one or more individual files. Existing relative paths are protected from accidental replacement, and removing a file requires confirmation.

## Bundles

A bundle is a reusable manifest composed of stored items. Create one from existing items:

```text
lasagna bundle create admin-ui navigation user-menu --destination Components
```

Add another item to an existing bundle at a different destination:

```text
lasagna bundle add admin-ui audit-service --destination Services
```

List bundle contents:

```text
lasagna bundle list admin-ui
```

Load the complete bundle:

```text
lasagna load admin-ui
```

Each bundle item can carry its own destination and namespace-rewrite settings. Bundle destinations are relative to the current working directory.

## Command reference

Every command also accepts `-v` or `--verbose` to show diagnostic details when it fails.

### `item`

Create or update a stored item:

```text
lasagna item create <name> <files> [options]
lasagna item update <name> <files> [options]
```

`item create` creates a new item from one or more files or directories. `item update` replaces an existing item from the current project. Other projects can pull the updated item with `load --overwrite`.

Options:

- `--source-namespace <NAMESPACE>` overrides the namespace automatically detected from the source project and files.
- `--no-companions` skips Razor code-behind, CSS, and JavaScript companion discovery and the related-file prompt.

The options can be combined when needed:

```text
lasagna item create card Components/Card.razor --no-companions
lasagna item update card Components/Card.razor --source-namespace MyApp.Components
lasagna item create card Components/Card.razor --source-namespace MyApp.Components --no-companions
```

#### File management

Manage the files inside an existing stored item:

```text
lasagna item list <name>
lasagna item add <name> <files> [options]
lasagna item remove <name> <files>
```

`item list` shows the relative paths currently stored. `item add` reads files or directories from the current working directory and adds them to the item; Razor companions are included unless `--no-companions` is supplied. `item remove` takes relative paths inside the item and can remove a file or an entire stored directory. An item must retain at least one file.

Examples:

```text
lasagna item list card
lasagna item add card Components/ValidationMessage.razor
lasagna item add card Components/ValidationMessage.razor --no-companions
lasagna item remove card Components/ValidationMessage.razor
```

### `load`

Copy an item, bundle, or kit into the current working directory.

```text
lasagna load <name> [options]
```

Options:

- `--overwrite` replaces existing destination files.
- `--destination <PATH>` places loaded files under a relative folder.
- `--namespace <NAMESPACE>` specifies the target namespace instead of using the current project's namespace.
- `--no-namespace-rewrite` copies namespaces exactly as stored.
- `--migrate` is a compatibility alias; namespace alignment is already automatic.

Common combinations:

```text
lasagna load card
lasagna load card --overwrite
lasagna load card --destination Components
lasagna load card --destination Components --overwrite
lasagna load card --namespace MyApp.Features
lasagna load card --destination Components --namespace MyApp.Features --overwrite
lasagna load card --destination Components --no-namespace-rewrite --overwrite
```

`--destination` is always relative to the current working directory. The load destination is added in front of any destination recorded in a bundle. `--namespace` only matters when namespace rewriting is enabled. `--no-namespace-rewrite` takes precedence over automatic or explicit namespace selection.

### `namespace`

Rename a namespace across an existing project. This is the explicit project-wide step for updating files that were
already present before a kit, item, or bundle was loaded:

```text
lasagna namespace rename <FROM> <TO>
lasagna namespace rename <FROM> <TO> --path src/Orders
```

The command previews changes by default. It recursively scans C#, Razor, and `.cshtml` files, rewrites the namespace
and all matching descendant namespaces, `using` directives, aliases, and fully qualified code references. Generated
files and common build or source-control directories are skipped. Use `--apply` to write the previewed changes:

```text
lasagna namespace rename Thing.Api MyCompany.OrderApi --dry-run
lasagna namespace rename Thing.Api MyCompany.OrderApi --apply
```

The rename uses complete namespace-prefix matching, so `Thing.Apiary` is not changed by a `Thing.Api` rename. It does
not modify comments, string literals, project files, or package references.

### `browse`

Browse stored content or open the local Lasagna web interface.

```text
lasagna browse web
lasagna browse item card
lasagna browse bundle admin-ui
lasagna browse kit orders-api
```

The item, bundle, and kit commands open stored files in separate browser tabs using local `file://` URLs. Bundle destinations do not affect browsing; the commands open the stored files themselves.

`browse web` starts the local web interface and opens it in the browser.

The server listens only on the local machine and keeps the command active while it runs. Stop the running command from its terminal. The interface uses the bundled Bootswatch United theme with Bootstrap 5.3 styling and JavaScript.

## Browser interface

The local browser interface is a dark-mode, server-rendered UI:

- `/` shows the library overview.
- `/items` creates and lists stored items.
- `/items/<name>` manages an item's files, opens individual files, and deletes the item.
- `/items/<name>/files/<path>` shows a read-only plain-text preview with line numbers and an option to open the raw file.
- `/bundles` creates and lists bundles.
- `/bundles/<name>` shows the items referenced by a bundle and allows the bundle to be deleted.
- `/kits` captures and lists directory-content kits.
- `/kits/<name>` shows the files captured by a kit, lets you add or remove individual files, and allows the kit to be deleted.
- `/namespace/rename` previews a recursive namespace rename with expandable Git-style diffs and requires an explicit confirmation before applying changes.
- `/docs` contains the in-app command and workflow reference.

The file preview intentionally uses readable plain text rather than full syntax highlighting. The top-bar Clear action requires confirmation before removing the local library. Form actions return server-rendered HTML and update the current page content without a client-side application shell.

### `list`

Display stored items, bundles, and kits in separate tables.

```text
lasagna list
lasagna list --items
lasagna list --bundles
lasagna list --kits
```

`--items`, `--bundles`, and `--kits` are mutually exclusive. Use plain `lasagna list` to show all tables.

### `remove`

Remove an item, bundle, or kit immediately.

```text
lasagna remove card
lasagna remove card --item
lasagna remove admin-ui --bundle
lasagna remove orders-api --kit
```

If neither `--item`, `--bundle`, nor `--kit` is supplied, Lasagna identifies the stored object automatically. The type switches are mutually exclusive.

### `clear`

Show and remove every stored item, bundle, and kit immediately.

```text
lasagna clear
```

### `kit`

Capture and manage directory-content kits. A kit command always starts with the `kit` branch:

```text
lasagna kit <command> [arguments] [options]
```

#### `kit create`

Capture a directory as a kit:

```text
lasagna kit create <name> <directory> [options]
```

Options:

- `--source-namespace <NAMESPACE>` overrides the namespace detected from the captured directory.

#### `kit update`

Replace an existing kit with the contents of a directory:

```text
lasagna kit update <name> <directory> [options]
```

#### `kit list`

List kits or show the files inside one:

```text
lasagna kit list
lasagna kit list <name>
```

#### `kit delete`

Delete a kit:

```text
lasagna kit delete <name>
```

### `bundle`

Manage bundle manifests. A bundle command always starts with the `bundle` branch:

```text
lasagna bundle <command> [arguments] [options]
```

#### `bundle create`

Create a bundle from one or more stored items.

```text
lasagna bundle create <name> <items> [options]
```

Options:

- `--destination <PATH>` places every initial item under a relative folder.
- `--namespace <NAMESPACE>` records a target namespace for every initial item.
- `--no-namespace-rewrite` keeps every initial item's stored namespaces unchanged when loaded.

These options can be combined:

```text
lasagna bundle create admin-ui navigation user-menu
lasagna bundle create admin-ui navigation user-menu --destination Components
lasagna bundle create admin-ui navigation user-menu --namespace MyApp.Components
lasagna bundle create admin-ui navigation user-menu --destination Components --namespace MyApp.Components
lasagna bundle create admin-ui navigation user-menu --destination Components --no-namespace-rewrite
```

#### `bundle list`

List bundle summaries, or show the item references inside one bundle.

```text
lasagna bundle list
lasagna bundle list <name>
```

Use the bundle name to see each referenced item, destination, and namespace setting:

```text
lasagna bundle list admin-ui
```

#### `bundle add`

Add one stored item reference to an existing bundle.

```text
lasagna bundle add <bundle> <item> [options]
```

Options:

- `--destination <PATH>` places the item under a relative folder.
- `--namespace <NAMESPACE>` records a target namespace for the item.
- `--no-namespace-rewrite` keeps the item's stored namespaces unchanged when loaded.

Examples:

```text
lasagna bundle add admin-ui audit-service
lasagna bundle add admin-ui audit-service --destination Services
lasagna bundle add admin-ui audit-service --namespace MyApp.Services
lasagna bundle add admin-ui audit-service --destination Services --namespace MyApp.Services
lasagna bundle add admin-ui audit-service --destination Services --no-namespace-rewrite
```

#### `bundle remove`

Remove an item reference from a bundle.

```text
lasagna bundle remove <bundle> <item> [options]
```

Options:

- `--destination <PATH>` removes only the reference at that destination. This is useful when the same item appears in a bundle more than once.

```text
lasagna bundle remove admin-ui audit-service
lasagna bundle remove admin-ui audit-service --destination Services
```

#### `bundle delete`

Delete a bundle manifest. The stored items referenced by the bundle are not deleted.

```text
lasagna bundle delete <name>
```

### Namespace and bundle option rules

- Source namespaces are detected while saving. `--source-namespace` is an override, not a requirement.
- A bundle item's `--namespace` setting overrides the namespace supplied to `load` for that item.
- Kits store their detected source namespace and use the same automatic namespace rewriting as items and bundles.
- `load --namespace` is used for items that do not have a bundle-specific target namespace.
- `--no-namespace-rewrite` on `load` disables rewriting for the entire load operation, including bundles.
- `--namespace` and `--no-namespace-rewrite` may be supplied together, but the explicit namespace has no effect while rewriting is disabled.
- `--overwrite` is independent of namespace options and can be combined with any load destination or namespace option.

## Terminal output

Lasagna uses plain, terminal-friendly output so it works in normal shells, Visual Studio's Package Manager Console, redirected output, and CI. Commands include a branded startup line, optional console colors, ASCII tables, file progress messages, and deterministic non-interactive destructive operations.
