#addin nuget:?package=Cake.Git&version=1.0.1
#addin nuget:?package=Newtonsoft.Json&version=13.0.1

using System.Net.Http;
using System.Linq;
using Newtonsoft.Json.Linq;

var IOSVERSION = Argument("iosversion", "13.3.3");
var IOS_SERVICERELEASE_VERSION = "0"; // This is combined with the IOSVERSION variable for the NuGet Package version

var target = Argument ("target", "Default");
var NUGET_API_KEY = EnvironmentVariable("NUGET_API_KEY");

Task ("DownloadDeps")
	.Description ("Downloads frameworks")
	.Does (async () => {
		Information ("Downloading all the dependencies please wait...");

		if (!IsRunningOnUnix ()) {
			Error ("*** This can only be run in macOS. ***");
			return;
		}

		var iosUrl = $"https://customers.pspdfkit.com/pspdfkit-ios/{IOSVERSION}-framework.podspec.json";
		var instantUrl = $"https://customers.pspdfkit.com/instant/{IOSVERSION}-framework.podspec.json";

		var iosDlUrl = await ResolveDownloadUrl (iosUrl);
		var instantDlUrl = await ResolveDownloadUrl (instantUrl);

		CreateDirectory("./cache");
		DownloadFile (iosDlUrl, $"./cache/ios.zip");
		DownloadFile (instantDlUrl, $"./cache/instant.zip");

		UnzipFile ($"./cache/ios.zip", $"./cache/ios");
		UnzipFile ($"./cache/instant.zip", $"./cache/ios");

		CopyDir ("./cache/ios/PSPDFKit.framework", "./PSPDFKit.iOS.Model/PSPDFKit.framework");
		CopyDir ("./cache/ios/PSPDFKitUI.framework", "./PSPDFKit.iOS.UI/PSPDFKitUI.framework");
		CopyDir ("./cache/ios/Instant.framework", "./PSPDFKit.iOS.Instant/Instant.framework");
	}
);

Task ("iOSModel")
	.Description ("Builds 'PSPDFKit.iOS.Model.dll', expects 'PSPDFKit.framework' inside './PSPDFKit.iOS.Model/' Directory\n")
	.Does (() => {
		Information ("=== PSPDFKit.iOS.Model.dll ===");
		if (!DirectoryExists ("./PSPDFKit.iOS.Model/PSPDFKit.framework/"))
			throw new Exception ("Unable to locate 'PSPDFKit.framework' inside './PSPDFKit.iOS.Model' Directory");

		MSBuild ("./PSPDFKit.iOS.Model/PSPDFKit.iOS.Model.csproj", new MSBuildSettings ()
			.SetConfiguration ("Release")
		);
		if (FileExists ("./PSPDFKit.iOS.Model/bin/Release/PSPDFKit.iOS.Model.dll"))
			CopyFile ("./PSPDFKit.iOS.Model/bin/Release/PSPDFKit.iOS.Model.dll", "./PSPDFKit.iOS.Model.dll");
	}
);

Task ("iOSUI")
	.Description ("Builds 'PSPDFKit.iOS.UI.dll', expects 'PSPDFKitUI.framework' inside './PSPDFKit.iOS.UI/' Directory\n")
	.Does (() => {
		Information ("=== PSPDFKit.iOS.UI.dll ===");
		if (!DirectoryExists ("./PSPDFKit.iOS.UI/PSPDFKitUI.framework/")) {
			Warning ("Unable to locate 'PSPDFKitUI.framework' inside './PSPDFKit.iOS.UI' Directory");
			Warning ("Skipping PSPDFKit.iOS.UI.dll");
		} else {
			MSBuild ("./PSPDFKit.iOS.UI/PSPDFKit.iOS.UI.csproj", new MSBuildSettings ()
				.SetConfiguration ("Release")
			);
			if (FileExists ("./PSPDFKit.iOS.UI/bin/Release/PSPDFKit.iOS.UI.dll"))
				CopyFile ("./PSPDFKit.iOS.UI/bin/Release/PSPDFKit.iOS.UI.dll", "./PSPDFKit.iOS.UI.dll");
		}
	}
);

Task ("iOSInstant")
	.Description ("Builds 'PSPDFKit.iOS.Instant.dll', expects 'Instant.framework' inside './PSPDFKit.iOS.Instant/' Directory\n")
	.Does (() => {
		Information ("=== PSPDFKit.iOS.Instant.dll ===");
		if (!DirectoryExists ("./PSPDFKit.iOS.Instant/Instant.framework/")) {
			Warning ("Unable to locate 'Instant.framework' inside './PSPDFKit.iOS.Instant' Directory");
			Warning ("Skipping PSPDFKit.iOS.Instant.dll");
		} else {
			MSBuild ("./PSPDFKit.iOS.Instant/PSPDFKit.iOS.Instant.csproj", new MSBuildSettings ()
				.SetConfiguration ("Release")
			);
			if (FileExists ("./PSPDFKit.iOS.Instant/bin/Release/PSPDFKit.iOS.Instant.dll"))
				CopyFile ("./PSPDFKit.iOS.Instant/bin/Release/PSPDFKit.iOS.Instant.dll", "./PSPDFKit.iOS.Instant.dll");
		}
	}
);

Task ("RestoreNugets")
	.Does (() => {
		Information ("=== Restoring NuGets ===");
		NuGetRestore ("./PSPDFKit.sln");
});

Task ("ios")
	.Description ("Builds iOS PSPDFKit dlls.\n")
	.IsDependentOn ("Clean")
	.IsDependentOn ("DownloadDeps")
	.IsDependentOn ("RestoreNugets")
	.IsDependentOn ("iOSModel")
	.IsDependentOn ("iOSUI")
	.IsDependentOn ("iOSInstant")
	.Does (() => {
		Information ("DONE! You will find the PSPDFKit.*.dll's in the root folder.");
	}
);

Task ("Default")
	.Description ("Builds all PSPDFKit dlls.\n")
	.IsDependentOn ("Clean")
	.IsDependentOn ("DownloadDeps")
	.IsDependentOn ("RestoreNugets")
	.IsDependentOn ("iOSModel")
	.IsDependentOn ("iOSUI")
	.IsDependentOn ("iOSInstant")
	.Does (() => {
		Information ("DONE! You will find the PSPDFKit.*.dll's in the root folder.");
	}
);

Task ("NuGet")
	.IsDependentOn ("Default")
	.Does (() =>
{
	if(!DirectoryExists ("./nuget/pkgs/"))
		CreateDirectory ("./nuget/pkgs");

	var head = GitLogTip ("./");
	var commit = head.Sha.Substring (0,7);

	NuGetPack ("./nuget/pspdfkit-ios-model.nuspec", new NuGetPackSettings {
		Version = $"{IOSVERSION}.{IOS_SERVICERELEASE_VERSION}+sha.{commit}",
		OutputDirectory = "./nuget/pkgs/",
		BasePath = "./",
		Properties = new Dictionary<string, string> {
			{"NoWarn", "NU5105"},
		}
	});

	NuGetPack ("./nuget/pspdfkit-ios-ui.nuspec", new NuGetPackSettings {
		Version = $"{IOSVERSION}.{IOS_SERVICERELEASE_VERSION}+sha.{commit}",
		OutputDirectory = "./nuget/pkgs/",
		BasePath = "./",
		Properties = new Dictionary<string, string> {
			{"NoWarn", "NU5105"},
		}
	});

	NuGetPack ("./nuget/pspdfkit-ios-instant.nuspec", new NuGetPackSettings {
		Version = $"{IOSVERSION}.{IOS_SERVICERELEASE_VERSION}+sha.{commit}",
		OutputDirectory = "./nuget/pkgs/",
		BasePath = "./",
		Properties = new Dictionary<string, string> {
			{"NoWarn", "NU5105"},
		}
	});

});

Task ("NuGet-Push")
	.Does (() =>
{	
	var iOSFullVersion = IOSVERSION;

 	if (IOS_SERVICERELEASE_VERSION != "0") {
 		iOSFullVersion = $"{IOSVERSION}.{IOS_SERVICERELEASE_VERSION}";
 	}
	
	// Get the path to the packages
	var modelPackage = $"./nuget/pkgs/PSPDFKit.iOS.Model.{iOSFullVersion}.nupkg";
 	var uiPackage = $"./nuget/pkgs/PSPDFKit.iOS.UI.{iOSFullVersion}.nupkg";
 	var instantPackage = $"./nuget/pkgs/PSPDFKit.iOS.Instant.{iOSFullVersion}.nupkg";

	// Push the packages
	NuGetPush(modelPackage, new NuGetPushSettings {
			Source = "https://api.nuget.org/v3/index.json",
			ApiKey = NUGET_API_KEY
	});

	NuGetPush(uiPackage, new NuGetPushSettings {
			Source = "https://api.nuget.org/v3/index.json",
			ApiKey = NUGET_API_KEY
	});

	NuGetPush(instantPackage, new NuGetPushSettings {
			Source = "https://api.nuget.org/v3/index.json",
			ApiKey = NUGET_API_KEY
	});

});

Task ("Clean")
	.Description ("Cleans the build.\n")
	.Does (() => {
		var nukeFiles = new [] {
			"./PSPDFKit.iOS.Model.dll",
			"./PSPDFKit.iOS.UI.dll",
			"./PSPDFKit.iOS.Instant.dll",
		};

		foreach (var file in nukeFiles) {
			Console.WriteLine (file);
			if (FileExists ($"{file}"))
				Nuke ($"{file}");
		}

		var projdirs = new [] {
			"./PSPDFKit.iOS.Model",
			"./PSPDFKit.iOS.UI",
			"./PSPDFKit.iOS.Instant",
			"./PSPDFCatalog/PSPDFCatalog",
			"./XamarinForms/iOS",
			"./XamarinForms/XFSample",
		};

		foreach (var proj in projdirs) {
			Console.WriteLine (proj);
			if (DirectoryExists ($"{proj}/bin/"))
				Nuke ($"{proj}/bin");

			if (DirectoryExists ($"{proj}/obj/"))
				Nuke ($"{proj}/obj");
		}

		var nukedirs = new [] {
			"./packages",
			"./nuget/pkgs",
			"./cache/ios",
			"./cache",
			"./PSPDFKit.iOS.Model/PSPDFKit.framework",
			"./PSPDFKit.iOS.UI/PSPDFKitUI.framework",
			"./PSPDFKit.iOS.Instant/Instant.framework",
		};

		foreach (var dir in nukedirs) {
			Console.WriteLine (dir);
			if (DirectoryExists ($"{dir}/"))
				Nuke ($"{dir}");
		}
	}
);

static HttpClient client = new HttpClient ();
static async Task<string> ResolveDownloadUrl (string url)
{
	var json = await client.GetStringAsync (url);
	var jobj = JObject.Parse (json);
	return (string) jobj["source"]["http"];
}

void LipoCreate (FilePath binaryPath, params FilePath [] thinBinaries)
{
	var args = string.Join (" ", thinBinaries.Select (i => $"\"{i}\""));
	StartProcess ("lipo", new ProcessSettings {
		Arguments = $"-create -output \"{binaryPath}\" {args}"
	});
}

void CopyDir (FilePath origin, FilePath destination)
{
	StartProcess ("cp", new ProcessSettings {
		Arguments = $"-RP \"{origin}\" \"{destination}\""
	});
}

void UnzipFile (FilePath zipFile, DirectoryPath destination)
{
	StartProcess ("unzip", new ProcessSettings {
		Arguments = $"\"{zipFile}\" -d \"{destination}\""
	});
}

void Nuke (string path)
{
	StartProcess ("rm", new ProcessSettings {
		Arguments = $"-rf \"{path}\""
	});
}

RunTarget (target);
