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

## Quick start

Save a file as an item:

```text
lasagna save card Components/Card.razor --source-namespace MyApp.Components
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

When a Razor file is selected, Lasagna automatically looks for its related files:

- `Component.razor`
- `Component.razor.cs`
- `Component.razor.css`
- `Component.razor.js`

To save only the explicitly selected files, use:

```text
lasagna save card Components/Card.razor --no-companions
```

If the files contain a namespace, record it when saving:

```text
lasagna save card Components/Card.razor --source-namespace MyApp.Components
```

## Loading items

Use `load` to copy an item or bundle into the current working directory:

```text
lasagna load logging
lasagna load admin-ui --overwrite
```

Existing files are protected by default. Use `--overwrite` when replacement is intentional.

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
lasagna bundle list
```

Load the complete bundle:

```text
lasagna load admin-ui
```

Each bundle item can carry its own destination and namespace-rewrite settings. Bundle destinations are relative to the current working directory.

## Command reference

### `save`

Save files or directories as a named item.

```text
lasagna save <name> <files> [options]
```

Options:

- `--source-namespace <NAMESPACE>` records the namespace used by the source files.
- `--no-companions` disables automatic Razor companion-file discovery.
- `--verbose` shows diagnostic details if the command fails.

### `load`

Copy an item or bundle into the current project.

```text
lasagna load <name> [options]
```

Options:

- `--overwrite` replaces existing destination files.
- `--namespace <NAMESPACE>` specifies the target namespace.
- `--no-namespace-rewrite` disables namespace rewriting.
- `--verbose` shows diagnostic details if the command fails.

### `list`

Display stored items and bundles in a table.

```text
lasagna list
lasagna list --items
lasagna list --bundles
```

### `remove`

Remove an item or bundle. Lasagna asks for confirmation unless `--force` is used.

```text
lasagna remove card
lasagna remove admin-ui --bundle --force
lasagna remove card --item --force
```

### `clear`

Show and remove every stored item and bundle.

```text
lasagna clear
lasagna clear --force
```

### `bundle`

Manage bundle manifests.

```text
lasagna bundle create <name> <items> [options]
lasagna bundle list
lasagna bundle add <bundle> <item> [options]
lasagna bundle remove <bundle> <item> [options]
lasagna bundle delete <name> [--force]
```

Bundle options include:

- `--destination <PATH>` places an item under a relative folder.
- `--namespace <NAMESPACE>` records a target namespace for the item.
- `--no-namespace-rewrite` keeps the item's stored namespaces unchanged.

## Terminal output

Lasagna uses Spectre.Console for its terminal experience. Commands include a branded startup display, colored success and error messages, tables for stored content, progress indicators for file operations, and confirmation prompts for destructive operations.
