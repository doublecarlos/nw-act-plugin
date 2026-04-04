#  Advanced Combat Tracker (ACT) plugin for Neverwinter
This repository was made to archive code for the NW ACT plugin.

# Basic Usage - For plugin users
* Download the file [Neverwinter.cs](Neverwinter.cs)
* On the "Plugins" tab of ACT, click "Browse"
* Select the downloaded file
* Click on "Add/Enable Plugin"

# Building/Testing the plugin - For plugin developers
With some help from Claude Code, now we have some basic unit/regression tests available for the plugin.

## Pre-requisites
ACT plugins are .NET Framework (not Core) projects, and must target .NET 4.8.

Simplest way to install the needed components is the following command: `winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --includeRecommended --quiet --wait"`

Alternatively, the Visual Studio Installer can be used, select workload ".NET desktop build tools" and check the component ".NET Framework 4.8 targeting pack".

Once installed, add the dotnet directory to PATH (typically `C:\Program Files\dotnet`).

## Running tests
On the project root, run: `dotnet test`

## Building the plugin
On the project root, run: `dotnet build`. This will run a debug build.

If your ACT is installed in a non-standard location, use `-p:ActPath` to specify it. Example: `dotnet build -p:ActPath="C:\Program Files (x86)\Advanced Combat Tracker\Advanced Combat Tracker.exe"`.

To perform a "release" build, add `-c Release`.

Once the build is done, you can find the plugin DLL at `Plugin/bin/Debug/net48/Plugin.dll` (or `Plugin/bin/Release/net48/Plugin.dll` for release builds). These can be loaded as plugins in ACT instead of the .cs file.

# See also
[Neverwinter-ACT-Plugin](https://github.com/nilsbrummond/Neverwinter-ACT-Plugin) - Original code by nilsbrummond