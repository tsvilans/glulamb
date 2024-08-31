using System.Reflection;
using System.Runtime.InteropServices;
using Rhino.PlugIns;

// Plug-in Description Attributes - all of these are optional
// These will show in Rhino's option dialog, in the tab Plug-ins
[assembly: PlugInDescription(DescriptionType.Email, "info@tomsvlians.com")]
[assembly: PlugInDescription(DescriptionType.Organization, "Tom Svilans")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "https://github.com/tsvilans/glulamb")]
[assembly: PlugInDescription(DescriptionType.WebSite, "http://www.tomsvilans.com/")]
[assembly: PlugInDescription(DescriptionType.Icon, "GluLamb.Resources.GluLamb.ico")]


// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("911C5517-96BC-4C6F-B529-3FA571010754")] // This will also be the Guid of the Rhino plug-in
