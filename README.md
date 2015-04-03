# Fix-Nuget-References
Whip nuget assembly references in your existing C# codebase into shape.

# What is this for?
I wrote this script to enable quick fixing of references to assemblies in a large number of projects.

Many 'older' projects will accumulate assembly references to external libraries. For a bunch of reasons, these references may not always be added correctly. For example:

- Developers use Resharper and just click Ok when it suggests to add an assembly reference
- It's an older project that predates Nuget and some third-party references survived and now need to be rewired to Nuget.
- There are multiple versions of the same library used in different parts of the code.

This script quickly (!) processes all projects and packages to rewire all those bad assembly references.
While you could use Nuget in VisualStudio to do this, Nuget is very, very slow on mass installs / updates. This script finishes in about 20 secs on a 200+ project code base on average developer hardware.

# What does it do?
This script will:
1. Scan all installed Nuget packages for assemblies
2. Scan all C# project files (`.csproj`) for references to these assemblies
3. For any matching assembly reference
  - Add or update the hint path
  - Add or update the `packages.config` file so Nuget knows about the package reference

This will achieve the following:
- All projects will correctly reference the same Nuget package for the same library
- Nuget knows about the usage of packages in each library, so you can properly use Nuget again to handle upgrading a package across the entire project.

# Usage

Make sure you have [ScriptCs](http://scriptcs.net/) installed and run:

`scriptcs Fix-Nuget-References.csx -- path\to\my-project`

# What you should know before running the script

- Make sure you can revert any change, usage is at your own risk (but you are using source control anyway, _right?_)
- Before running the script, it's recommended to upgrade to [new style Nuget package restore](http://blog.davidebbo.com/2014/01/the-right-way-to-restore-nuget-packages.html). Here's a [handy script](https://github.com/owen2/AutomaticPackageRestoreMigrationScript) for this.
- The script assumes you have all Nuget packages in a `Packages` folder in the root of your application.
- If you have multiple versions of the same package installed, remove all the versions you don't want to use first.
- This is a brute force script and it doesn't handle Nuget install scripts or any other fancy stuff like that
- The script currently assumes you are targeting .NET 4.5
- There is absolutely no support for managing multiple versions of the same assembly
