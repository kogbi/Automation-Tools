# WormholeAutomationUI

A WPF UI for configuring and running an automation flow.

## Build

```bash
dotnet build
```

## Run

```bash
dotnet run
```

## Packaging (MSIX)

1. Publish a self-contained release build:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

2. Open MSIX Packaging Tool and choose "Create package" -> "From classic app".
3. Set the executable to `publish\WormholeAutomationUI.exe`.
4. Use `Assets\AppIcon.ico` for the package icon.
5. Generate the `.msix` file and distribute it for install.

## Template Images

- Use the Template section to browse, paste (Ctrl+V), or drag-and-drop an image.
- Pasted/dragged images are kept in memory only and are not saved to disk.

## Language

Use the top-right language button to switch between Chinese and English UI.
