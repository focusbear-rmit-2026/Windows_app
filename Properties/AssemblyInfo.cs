using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
 
// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Focus Bear")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
#if E2E
// The isolated E2E test build (see Utils\E2EBuild.cs). The metadata is what FocusBear.Core.AppInstance reads; the
// product name is what the test runner checks before it launches an exe.
[assembly: AssemblyProduct("Focus Bear (E2E)")]
[assembly: AssemblyMetadata("FocusBearBuild", "E2E")]
#else
[assembly: AssemblyProduct("Focus Bear")]
#endif
[assembly: AssemblyCopyright("Copyright © 2022-2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
 

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The seam between the WPF app and FocusBear.Core is tested headlessly; see App.Tests.
[assembly: InternalsVisibleTo("FocusBear.App.Tests")]

//In order to begin building localizable applications, set
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                                     //(used if a resource is not found in the page,
                                     // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
                                              //(used if a resource is not found in the page,
                                              // app, or any theme specific resource dictionaries)
)]


// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("4.1.252.1")]
[assembly: AssemblyFileVersion("4.1.252.1")]



    