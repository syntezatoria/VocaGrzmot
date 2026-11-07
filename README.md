# Grzmoto Editor

Dark-themed C# WPF editor for VocaGrzmot `grzmoto.json` files.

## Requirements
Windows 10/11 and .NET 8 SDK with WPF support.

## Run
```powershell
dotnet run --project GrzmotoEditor.csproj
```

## Features
- Open/save native `grzmoto.json` files
- Search aliases and filenames
- Add, edit and delete phoneme entries
- Edit absolute timing boundaries in milliseconds
- Validate that each alias has one more boundary than phonemes

This is a first GUI foundation, not yet a waveform editor or Vocaloid exporter.
