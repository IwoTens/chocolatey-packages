///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

var nugetDir = Directory("./.nuget");
var chocolateyPackSettings = new ChocolateyPackSettings {
    OutputDirectory = nugetDir
};

var revertFolderList = new List<string>();
///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Teardown(context =>
{
    // Revert the backup directories to the previous version without replacements
    foreach (var folder in revertFolderList) {
        var origName = folder.Remove(folder.Length - 4);
        DeleteDirectory(origName, new DeleteDirectorySettings { Recursive = true, Force = true });
        MoveDirectory(folder, origName);
    }
});

Task("Clean-Output")
    .Does(() =>
{
    CleanDirectory(nugetDir);
});

Task("Pack-Flyway")
    .IsDependentOn("Clean-Output")
    .Does(() =>
{
    var version = "9.17.0";

    // Handle the file without jre
    {
        var packageName = "flyway.commandline";
        var hash = GetOnlineFileHash($"https://repo1.maven.org/maven2/org/flywaydb/flyway-commandline/{version}/flyway-commandline-{version}.zip");
        ReplaceInFiles(packageName, new Dictionary<string, string> {
            ["{version}"] = version,
            ["{checksum}"] = hash,
            ["{year}"] = $"{DateTime.Now.Year}",
        });
        ChocolateyPack($"./{packageName}/{packageName}.nuspec", chocolateyPackSettings);
    }

    // Handle the file with JRE
    {
        var packageName = "flyway.commandline.withjre";
        var hash = GetOnlineFileHash($"https://repo1.maven.org/maven2/org/flywaydb/flyway-commandline/{version}/flyway-commandline-{version}-windows-x64.zip");
        ReplaceInFiles(packageName, new Dictionary<string, string> {
            ["{version}"] = version,
            ["{checksum}"] = hash,
            ["{year}"] = $"{DateTime.Now.Year}",
        });
        ChocolateyPack($"./{packageName}/{packageName}.nuspec", chocolateyPackSettings);
    }
});

Task("Pack-FreeRDP")
    .IsDependentOn("Clean-Output")
    .Does(() =>
{
    ChocolateyPack("./freerdp/freerdp.nuspec", chocolateyPackSettings);
});

Task("Pack-SonarQube-Scanner")
    .IsDependentOn("Clean-Output")
    .Does(() =>
{
    var version = "4.8.0.2856";

    var packageName = "sonarqube-scanner.portable";
    var hash = GetOnlineFileHash($"https://binaries.sonarsource.com/Distribution/sonar-scanner-cli/sonar-scanner-cli-{version}-windows.zip");
    ReplaceInFiles(packageName, new Dictionary<string, string> {
        ["{version}"] = version,
        ["{checksum}"] = hash,
        ["{year}"] = $"{DateTime.Now.Year}",
    });
    ChocolateyPack($"./{packageName}/{packageName}.nuspec", chocolateyPackSettings);
});

Task("Pack-SqlServer-ODBC")
    .IsDependentOn("Clean-Output")
    .Does(() =>
{
    // see https://learn.microsoft.com/en-us/sql/connect/odbc/download-odbc-driver-for-sql-server?view=sql-server-ver16
    var version = "18.2.2.1";
    var link32 = "https://download.microsoft.com/download/c/5/4/c54c2bf1-87d0-4f6f-b837-b78d34d4d28a/en-US/18.2.2.1/x86/msodbcsql.msi";
    var link64 = "https://download.microsoft.com/download/c/5/4/c54c2bf1-87d0-4f6f-b837-b78d34d4d28a/en-US/18.2.2.1/x64/msodbcsql.msi";
    var hash32 = GetOnlineFileHash(link32);
    var hash64 = GetOnlineFileHash(link64);

    var packageName = "sqlserver-odbcdriver";
    ReplaceInFiles(packageName, new Dictionary<string, string> {
        ["{version}"] = version,
        ["{link32}"] = link32,
        ["{link64}"] = link64,
        ["{checksum32}"] = hash32,
        ["{checksum64}"] = hash64
    });
    ChocolateyPack($"./{packageName}/{packageName}.nuspec", chocolateyPackSettings);
});

Task("Pack-SqlServer-Sqlcmd")
    .IsDependentOn("Clean-Output")
    .Does(() =>
{
    var version = "15.0.4298.1";
    var link32 = "https://download.microsoft.com/download/a/a/4/aa47b3b0-9f67-441d-8b00-e74cd845ea9f/EN/x86/MsSqlCmdLnUtils.msi";
    var link64 = "https://download.microsoft.com/download/a/a/4/aa47b3b0-9f67-441d-8b00-e74cd845ea9f/EN/x64/MsSqlCmdLnUtils.msi";
    var hash32 = GetOnlineFileHash(link32);
    var hash64 = GetOnlineFileHash(link64);
    
    var packageName = "sqlserver-cmdlineutils";
    ReplaceInFiles(packageName, new Dictionary<string, string> {
        ["{version}"] = version,
        ["{link32}"] = link32,
        ["{link64}"] = link64,
        ["{checksum32}"] = hash32,
        ["{checksum64}"] = hash64
    });
    ChocolateyPack($"./{packageName}/{packageName}.nuspec", chocolateyPackSettings);
});

Task("Push-Packages")
    .Does(() =>
{
    var apiKey = System.IO.File.ReadAllText(".chocoapikey");

    var files = GetFiles($"{nugetDir}/*.nupkg");
    foreach (var package in files) {
        Information($"Pushing {package}");
        ChocolateyPush(package, new ChocolateyPushSettings {
            ApiKey = apiKey
        });
    }
});

Task("Default")
    .Does(() =>
{
    Information("Hello Cake!");
});

RunTarget(target);

/// <summary>
/// Downloads the given file and calculates the SHA265 hash of it.
/// </summary>
private string GetOnlineFileHash(string fileUrl) {
    var file = DownloadFile(fileUrl);
    var hash = CalculateFileHash(file, HashAlgorithm.SHA256).ToHex();
    return hash;
}

/// <summary>
/// Method that replaces all files in a folder with the given key/value placehoders.
/// Creates a backup of the folder before the modifications.
/// On Teardown, this backup is restored.
/// </summary>
private void ReplaceInFiles(string baseDirectory, Dictionary<string, string> replacements) {
    // Copy the original directory
    var backupFolderName = $"{baseDirectory}_bak";
    CleanDirectory(backupFolderName);
    CopyDirectory(baseDirectory, backupFolderName);
    revertFolderList.Add(backupFolderName);

    // Perform all the replacements
    string[] files = System.IO.Directory.GetFiles(baseDirectory, "*.*", SearchOption.AllDirectories);
    foreach (var file in files) {
        // Replace the content
        string contents = System.IO.File.ReadAllText(file);
        contents = contents.Replace(@"Text to find", @"Replacement text");
        foreach(var replacement in replacements)
        {
            contents = contents.Replace(replacement.Key, replacement.Value);
        }
        System.IO.File.WriteAllText(file, contents);
    }
}
