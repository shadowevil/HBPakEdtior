# HBPakEditor

## Overview  
**HBPakEditor** is a Windows Forms application built in C# that provides a GUI for reading, editing and saving *.PAK* archive files. It leverages the `PAKLib` library (via NuGet package version 1.1.0) to parse and manipulate the archive format.  
  
## Key Features  
- Open existing PAK archives, inspect the file entries and metadata.  
- Edit file names, replace, add or remove entries inside the archive.  
- Save changes back into a PAK file (preserving structure and offsets via PAKLib).  
- Built around a tabbed interface (one tab per archive), supporting deep inspection and batch operations.  
- Makes use of the open‑source [PAKLib](https://github.com/shadowevil/PAKLib) for all low‑level PAK format handling and integration.

## Architecture & Dependencies  
- Uses the NuGet package **PAKLib v1.1.0** to handle archive parsing, rebuilding and I/O.  
- The UI project includes various WinForms controls: `MainWindow`, `PAKTabPage`, `InputBox`, `RenamableTabControl`, etc.  
- The project file is `HBPakEditor.csproj`, solution `HBPakEditor.sln`.  
- `FileSignatureDetector.cs` enables automatic detection of supported archive file types.  
- Designed for .NET Framework (or any supported target configured in the csproj).

## Getting Started  
1. Clone the repository:
   ```bash
   git clone https://github.com/shadowevil/HBPakEdtior.git
   cd HBPakEdtior
   ```  
2. Open `HBPakEditor.sln` in Visual Studio.  
3. Restore NuGet packages (ensuring `PAKLib 1.1.0` is installed).  
   ```xml
   <PackageReference Include="PAKLib" Version="1.1.0" />
   ```  
4. Build and run the application.  
5. Use *File → Open* to load a `.PAK` archive, view contents, perform edits, then *Save As* to export the modified archive.

## Contribution & License  
- This project, along with `PAKLib`, is developed by the author *ShadowEvil*.  
- Feel free to report issues or submit pull‑requests for new features or bug‑fixes.  
- Licensing information is provided in the repo (please refer to the LICENSE file if included).

## Summary  
HBPakEditor offers a focused, user‑friendly interface for working with PAK archives, relying on the robust PAKLib library for format handling. Ideal for developers or modders needing to inspect or edit custom archive files with ease.
