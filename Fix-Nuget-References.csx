using System.Text.RegularExpressions;
using System.Xml.Linq;

if( Env.ScriptArgs.Count == 0 )
{
	throw new InvalidOperationException("Please provide the base path as an argument.");
}

string basePath = Path.GetFullPath(Env.ScriptArgs[0]);
if( !basePath.EndsWith("\\"))
  basePath += "\\";
if( !Directory.Exists( basePath ))
{
	throw new InvalidOperationException("Base path doesn't exist :"+ basePath );
}

string nugetPackagesBase = "Packages"; // Todo - look for nuget.config and get the package path from there

if( !Directory.Exists( Path.Combine( basePath, nugetPackagesBase ) ))
{
	throw new InvalidOperationException("Nuget packages not found in default location." );
}

var packagesToAdd = ScanPackages( Path.Combine( basePath, nugetPackagesBase ) ).ToArray();

Console.WriteLine("Found {0} assemblies provided by Nuget packages", packagesToAdd);
if( packagesToAdd.Length == 0)
	throw new InvalidOperationException("No packages found to process." );

static XNamespace  xmlns = "http://schemas.microsoft.com/developer/msbuild/2003";

var ProjectsProcessed = 0;
var	MissingPackageConfig = 0;

foreach( var projectFile in Directory.EnumerateFiles( basePath, "*.csproj", SearchOption.AllDirectories ))
{
	var xdoc = XDocument.Load( projectFile );

	var projectRelativePath = Path.Combine(
			Regex.Replace( System.IO.Path.GetDirectoryName( projectFile ).Substring( basePath.Length ), "[^\\\\]+", ".." ),
			nugetPackagesBase );

  bool hasChanges = false;
  ProjectsProcessed++;
	Console.WriteLine( "Processing "+projectFile.Substring(basePath.Length) );

	var packagesInProject = new List<NugetPackage>();

	// enumerate references, match agains list of assemblies
	foreach( var package in packagesToAdd )
	{
		 var reference = FindReference( xdoc, package.Assembly );
		 if( reference != null )
		 {
			  packagesInProject.Add( package );
				var assemblyPath = Path.Combine( projectRelativePath, package.HintPath );
		    if( !string.Equals(reference.HintPath, assemblyPath, StringComparison.OrdinalIgnoreCase ) )
		    {
			 		Console.WriteLine("\tUpdating hint path for package "+package.Name);
					reference.SetNugetReference( assemblyPath );
					hasChanges = true;
			  }
		 }
	}


  if( packagesInProject.Count == 0 )
	  continue;


  var packagesConfigPath = Path.Combine( Path.GetDirectoryName(projectFile), "packages.config" );
  XDocument packagesConfig;

  // see if there's a packages.config file
	if( !File.Exists( packagesConfigPath ) )
	{
		// if not create it
		MissingPackageConfig++;
		Console.WriteLine("\tpackages.config is missing");
		packagesConfig = new XDocument();
		packagesConfig.Add( new XElement( "packages" ));
  }
	else
	{
		packagesConfig = XDocument.Load(packagesConfigPath);
	}

	// ensure packages are in the packages.config
	foreach( var package in packagesInProject )
	{
		var packageElement = packagesConfig.Descendants("package").FirstOrDefault( r => r.Attribute("id").Value == package.Name );
		if( null == packageElement )
		{
			packageElement = new XElement("package",
				new XAttribute("id", package.Name),
				new XAttribute("version", package.Version),
				new XAttribute("targetFramework", package.targetFramework) );
			packagesConfig.Root.Add( packageElement );
		}

		packageElement.SetAttributeValue( "version", package.Version );
		packageElement.SetAttributeValue( "targetFramework", package.targetFramework );

	}
  packagesConfig.Save( packagesConfigPath, SaveOptions.None);

	// update csproj to have the correct hint path
	xdoc.Save( projectFile, SaveOptions.None );
}

Console.WriteLine("Processed {0} project files", ProjectsProcessed );
Console.WriteLine("\tadded {0} packages.config files", MissingPackageConfig );

public class NugetPackage
{
		public string Assembly { get; set; }
		public string Name  {get; set;}
		public string Version {get; set;}
		public string targetFramework {get; set;}
		public string AssemblyLocation { get; set;}
		public string HintPath{
			get{
				var subPath = AssemblyLocation ?? ( "lib\\"+targetFramework );

				return Path.Combine( Name+"."+Version, subPath, Assembly+".dll" );
			}
		}
}

public class Reference
{
	private readonly XElement _element;
	public Reference( XElement element )
	{
		_element = element;
	}

	public string HintPath{
		get{
			var hint = _element.Element(xmlns+"HintPath");
			return hint != null ? hint.Value : "";
		}
	}

  public void SetHintPath( string path )
	{
		var hint = _element.Element(xmlns+"HintPath");
		if( null == hint )
		{
			hint = new XElement( xmlns+"HintPath" );
			_element.Add( hint );
		}
		hint.Value = path;
	}

	public void SetNugetReference( string path )
	{
		SetHintPath( path );

    if( AssemblyHasVersion ){
			var specificVersion = _element.Element(xmlns+"SpecificVersion");
			if( null == specificVersion )
			{
				specificVersion = new XElement( xmlns+"SpecificVersion" );
				_element.Add( specificVersion );
			}
			specificVersion.Value = "False";
		}
	}

  public bool AssemblyHasVersion
	{
		get{
			return _element.Attribute("Include").Value.Split(',').Length > 0; // quick & dirty
		}
	}
}


public static Reference FindReference( XDocument csproj, string assemblyName )
{
	var element = csproj.Descendants(xmlns+"Reference").FirstOrDefault( r => string.Equals( r.Attribute("Include").Value.Split(',')[0], assemblyName, StringComparison.OrdinalIgnoreCase ) );
	return element != null ? new Reference( element ) : null;
}

public static IEnumerable<NugetPackage> ScanPackages( string packageBaseFolder )
{
	foreach( var packageFolder in Directory.EnumerateDirectories( packageBaseFolder ))
	{
	    var packageFolderName =Path.GetFileName( packageFolder );

	    var match = Regex.Match( packageFolderName, "(?:\\.)(?<version>[0-9\\.]+(-.*)?)" );
	    if( match.Success )
	    {
	      var version = match.Groups["version"].Value;
	      var package = packageFolderName.Substring(0, packageFolderName.Length - version.Length - 1);
	      string targetFolder = null;

	      // look for preferred version
	      var preferredVersions = new []{ "net45", "net40", "net20", "" };

	      foreach( var preferred in preferredVersions )
	      {
	        var libPath = Path.Combine( packageFolder, "lib", preferred );

	        if( Directory.Exists( libPath ) && Directory.EnumerateFiles( libPath, "*.dll", SearchOption.TopDirectoryOnly ).Any() )
	        {

	          targetFolder = preferred;
	          break;
	        }
	      }

	      if( null == targetFolder )
	      {
	        Console.WriteLine("Skipping {0} {1}", package, version);
	        continue;
	      }

	      foreach( var assembly in Directory.EnumerateFiles( Path.Combine( packageFolder, "lib", targetFolder ), "*.dll", SearchOption.TopDirectoryOnly ))
	      {
				  yield return new NugetPackage{
						Assembly = Path.GetFileNameWithoutExtension( assembly ),
						Name = package,
						Version = version,
						targetFramework = "net45",
						AssemblyLocation = "lib" + (!string.IsNullOrEmpty(targetFolder)? "\\"+targetFolder : "")
					};
	      }

	    }
	}
}
