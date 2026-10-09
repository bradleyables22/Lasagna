# Lasagna

Lasagna is a .NET command-line tool for saving and reusing files in .NET projects.

It is intended for project building blocks that are useful across projects but do not need a complete project template. Save a Razor component, service, configuration file, or related group of files once, then pull it into another project whenever you need it.

Files copied into a project remain ordinary project files. Lasagna does not keep them linked to the stored copy, so you can change them freely after loading them.

## How it works

Lasagna has two types of reusable content:

- **Items** are named collections of files and folders.
- **Bundles** are manifest files that reference multiple items and specify where each item should be placed.

Items and bundle manifests are stored in the user's local application-data directory under the Lasagna storage folder. A bundle references items by name rather than duplicating their files.

When files are loaded, Lasagna works from the current working directory. Relative paths in bundle manifests are resolved against that directory.

Command names, command options, item names, and bundle names are case-insensitive. File paths follow the rules of the operating system, while namespace text keeps C# and Razor's normal casing semantics.

## Quick start

Save a file as an item:

```text
lasagna save card Components/Card.razor
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

## Saving items

Use `save` with a name and one or more files or directories:

```text
lasagna save logging Logging/LoggingService.cs
lasagna save admin-ui Components/Admin Services/AdminService.cs
lasagna save shared-components Components/Shared
```

Selecting a directory preserves its folder structure inside the stored item.

When a Razor file is selected, Lasagna looks for related files:

- `Component.razor`
- `Component.razor.cs`
- `Component.razor.css`
- `Component.razor.js`

When related files are found, Lasagna lists them with numbers and includes them by default. Enter the numbers you want to exclude, separated by commas, or press Enter to include everything. In non-interactive terminals, discovered companions are included automatically.

To skip companion discovery entirely and save only the explicitly selected files, use:

```text
lasagna save card Components/Card.razor --no-companions
```

Lasagna automatically detects the source namespace from the nearest project and the selected source files. You can override that detection for unusual project layouts:

```text
lasagna save card Components/Card.razor --source-namespace MyApp.Components
```

## Loading items

Use `load` to copy an item or bundle into the current working directory:

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

### `save`

Save one or more files or directories as a named item.

```text
lasagna save <name> <files> [options]
```

Options:

- `--source-namespace <NAMESPACE>` overrides the namespace automatically detected from the source project and files.
- `--no-companions` skips Razor code-behind, CSS, and JavaScript companion discovery and the related-file prompt.
- `--push` updates an existing stored item from the current project instead of creating a new item.

The options can be combined when needed:

```text
lasagna save card Components/Card.razor --no-companions
lasagna save card Components/Card.razor --source-namespace MyApp.Components
lasagna save card Components/Card.razor --source-namespace MyApp.Components --no-companions
lasagna save card Components/Card.razor --push
```

Use `--push` after editing a loaded item in another project. Other projects can pull the updated item with `load --overwrite`.

### `load`

Copy an item or bundle into the current working directory.

```text
lasagna load <name> [options]
```

Options:

- `--overwrite` replaces existing destination files.
- `--destination <PATH>` places loaded files under a relative folder.
- `--namespace <NAMESPACE>` specifies the target namespace instead of using the current project's namespace.
- `--no-namespace-rewrite` copies namespaces exactly as stored.

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

### `inspect`

Open every file in an item or bundle in separate browser tabs using local `file://` URLs.

```text
lasagna inspect card
lasagna inspect admin-ui
```

Bundle destinations do not affect inspection; the command opens the stored files themselves.

### `list`

Display stored items and bundles in separate tables.

```text
lasagna list
lasagna list --items
lasagna list --bundles
```

`--items` and `--bundles` are mutually exclusive. Use plain `lasagna list` to show both tables.

### `remove`

Remove an item or bundle immediately.

```text
lasagna remove card
lasagna remove card --item
lasagna remove admin-ui --bundle
```

If neither `--item` nor `--bundle` is supplied, Lasagna identifies the stored object automatically. The two type switches are mutually exclusive.

### `clear`

Show and remove every stored item and bundle immediately.

```text
lasagna clear
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
- `load --namespace` is used for items that do not have a bundle-specific target namespace.
- `--no-namespace-rewrite` on `load` disables rewriting for the entire load operation, including bundles.
- `--namespace` and `--no-namespace-rewrite` may be supplied together, but the explicit namespace has no effect while rewriting is disabled.
- `--overwrite` is independent of namespace options and can be combined with any load destination or namespace option.

## Terminal output

Lasagna uses plain, terminal-friendly output so it works in normal shells, Visual Studio's Package Manager Console, redirected output, and CI. Commands include a branded startup line, optional console colors, ASCII tables, file progress messages, and deterministic non-interactive destructive operations.
