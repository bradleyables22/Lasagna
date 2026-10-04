# Lasagna

Lasagna will be a .NET CLI tool for saving and reusing individual files or groups of files in .NET projects.

The goal is to make small, reusable project building blocks easy to share without requiring a full project template. For example, a Razor component, service, configuration file, or related bundle could be saved once and added to another project later. The copied files remain normal project files that users can edit for their needs.

## Planned workflow

```text
lasagna save razor-card ./Components/RazorCard.razor
lasagna add razor-card
lasagna list
```

Bundles will support reusable groups of related files:

```text
lasagna bundle create auth-ui ./Components/Auth ./Services/AuthService.cs
lasagna bundle add auth-ui
```

## Project status

Lasagna is in early development. The project is configured as a .NET tool package named `Lasagna`, with `lasagna` as its command name. The CLI will use Spectre.Console for command parsing and terminal output.
