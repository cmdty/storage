using System.IO.Compression;
using System.Runtime.InteropServices;

var target = Argument<string>("Target", "Default");
var configuration = Argument<string>("Configuration", "Release");
bool publishWithoutBuild = Argument<bool>("PublishWithoutBuild", false);
string nugetPrereleaseTextPart = Argument<string>("PrereleaseText", "alpha");
string targetFramework = Argument<string>("TargetFramework", "net461");

bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
Information("OS Description: " + System.Runtime.InteropServices.RuntimeInformation.OSDescription);

string pythonCommand = isWindows ? "python" : "python3";
string shellCommand = isWindows ? "cmd" : "bash";
var artifactsDirectory = Directory("./artifacts");
var testResultDir = "./temp/";
string vsBuildOutputDirectory = System.IO.Path.Combine(".", "src", "Cmdty.Storage.Excel", "bin", configuration, targetFramework);
var isRunningOnBuildServer = !BuildSystem.IsLocalBuild;

var msBuildSettings = new DotNetCoreMSBuildSettings();

// Maps text used in prerelease part in NuGet package to PyPI package
var prereleaseVersionTextMapping = new Dictionary<string, string>
{
	{"alpha", "a"},
	{"beta", "b"},
	{"rc", "rc"}
};

string pythonPrereleaseTextPart = prereleaseVersionTextMapping[nugetPrereleaseTextPart];

msBuildSettings.WithProperty("PythonPreReleaseTextPart", pythonPrereleaseTextPart);

if (HasArgument("PrereleaseNumber"))
{
    msBuildSettings.WithProperty("PrereleaseNumber", Argument<string>("PrereleaseNumber"));
    msBuildSettings.WithProperty("VersionSuffix", nugetPrereleaseTextPart + Argument<string>("PrereleaseNumber"));
}

if (HasArgument("VersionPrefix"))
{
    msBuildSettings.WithProperty("VersionPrefix", Argument<string>("VersionPrefix"));
}

if (HasArgument("PythonVersion"))
{
    msBuildSettings.WithProperty("PythonVersion", Argument<string>("PythonVersion"));
}

Task("Clean-Artifacts")
    .Does(() =>
{
    CleanDirectory(artifactsDirectory);
});

Task("Build")
	.IsDependentOn("Clean-Artifacts") // Necessary as msbuild tasks in Cmdty.Storage.Excel.csproj copy the add-ins into the artifacts directory
    .Does(() =>
{
    var dotNetCoreSettings = new DotNetCoreBuildSettings()
            {
                Configuration = configuration,
                MSBuildSettings = msBuildSettings
            };
    string solutionName = isWindows ? "Cmdty.Storage.sln" : "Cmdty.Storage.XPlat.sln";
    DotNetCoreBuild(solutionName, dotNetCoreSettings);
});

Task("Test-C#")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("Cleaning test output directory");
    CleanDirectory(testResultDir);

    var projects = GetFiles("./tests/**/*.Test.csproj");
    
    foreach(var project in projects)
    {
        Information("Testing project " + project);
        DotNetCoreTest(
            project.ToString(),
            new DotNetCoreTestSettings()
            {
                ArgumentCustomization = args=>args.Append($"/p:CollectCoverage=true /p:CoverletOutputFormat=cobertura"),
                Logger = "trx",
                ResultsDirectory = testResultDir,
                Configuration = configuration,
                NoBuild = true
            });
    }
});

string vEnvPath = System.IO.Path.Combine("src", "Cmdty.Storage.Python", "storage-venv");
string venvActiveFolderAndFile = isWindows ? System.IO.Path.Combine("Scripts", "activate.bat") :
                                             System.IO.Path.Combine("bin", "activate");
string vEnvActivatePath = System.IO.Path.Combine(vEnvPath, venvActiveFolderAndFile);

Task("Create-VirtualEnv")
    .Does(() =>
{
    if (System.IO.File.Exists(vEnvActivatePath))
    {
        Information("storage-venv Virtual Environment already exists, so no need to create.");
    }
    else
    {
        Information("Creating storage-venv Virtual Environment.");
        StartProcessThrowOnError(pythonCommand, "-m venv " + vEnvPath);
    }
});

Task("Install-VirtualEnvDependencies")
	.IsDependentOn("Create-VirtualEnv")
    .Does(() =>
{
    RunCommandInVirtualEnv(pythonCommand + " -m pip install --upgrade pip", vEnvActivatePath);
    RunCommandInVirtualEnv("pip install pytest", vEnvActivatePath);
    RunCommandInVirtualEnv("pip install -r src/Cmdty.Storage.Python/requirements.txt", vEnvActivatePath);
    RunCommandInVirtualEnv("pip install -e src/Cmdty.Storage.Python", vEnvActivatePath);
});

var testPythonTask = Task("Test-Python")
    .IsDependentOn("Install-VirtualEnvDependencies")
	.IsDependentOn("Test-C#")
	.Does(() =>
{
    RunCommandInVirtualEnv(pythonCommand + " -m pytest src/Cmdty.Storage.Python/tests --junitxml=junit/test-results.xml", vEnvActivatePath);
});

Task("Build-Samples")
	.Does(() =>
{
	var dotNetCoreSettings = new DotNetCoreBuildSettings()
        {
            Configuration = configuration,
        };
	DotNetCoreBuild("samples/csharp/Cmdty.Storage.Samples.sln", dotNetCoreSettings);
});

Task("Pack-NuGet")
	.IsDependentOn("Build-Samples")
	.IsDependentOn("Test-C#")
	.Does(() =>
{
	var dotNetPackSettings = new DotNetCorePackSettings()
                {
                    Configuration = configuration,
                    OutputDirectory = artifactsDirectory,
                    NoRestore = true,
                    NoBuild = true,
                    MSBuildSettings = msBuildSettings
                };
	DotNetCorePack("src/Cmdty.Storage/Cmdty.Storage.csproj", dotNetPackSettings);
});	

Task("Pack-Python")
    .IsDependentOn("Test-Python")
    .IsDependentOn("Build")
	.Does(setupContext =>
{
    CleanDirectory("src/Cmdty.Storage.Python/build");
    CleanDirectory("src/Cmdty.Storage.Python/dist");
    var originalWorkingDir = setupContext.Environment.WorkingDirectory;
    string pythonProjDir = System.IO.Path.Combine(originalWorkingDir.ToString(), "src", "Cmdty.Storage.Python");
    setupContext.Environment.WorkingDirectory = pythonProjDir;
    try
    {    
        StartProcessThrowOnError(pythonCommand, "setup.py", "bdist_wheel");
    }
    finally
    {
        setupContext.Environment.WorkingDirectory = originalWorkingDir;
    }
    Information("Python package created");
    CopyFiles("src/Cmdty.Storage.Python/dist/*", artifactsDirectory);
    Information("Python package file copied to /artifacts directory");
});

Task("Copy-Bins")
    .Does(setupContext =>
{
    //Information("Copying files from " + vsBuildOutputDirectory + "/*.dll" + " to " +vsBuildOutputDirectory + "/x86/" );
    string x86Folder = System.IO.Path.Combine(vsBuildOutputDirectory, "x86");
    string x64Folder = System.IO.Path.Combine(vsBuildOutputDirectory, "x64");
    string allDllsGlob = System.IO.Path.Combine(vsBuildOutputDirectory, "*.dll");
    string allPdbsGlob = System.IO.Path.Combine(vsBuildOutputDirectory, "*.pdb");

    CopyFiles(allDllsGlob , x86Folder);
    CopyFiles(allDllsGlob , x64Folder);
    CopyFiles(allPdbsGlob , x86Folder);
    CopyFiles(allPdbsGlob , x64Folder);
    CopyFiles(System.IO.Path.Combine(vsBuildOutputDirectory, "Cmdty.Storage.Excel-AddIn.xll"), x86Folder);
    CopyFiles(System.IO.Path.Combine(vsBuildOutputDirectory, "Cmdty.Storage.Excel-AddIn.dna"), x86Folder);
    CopyFiles(System.IO.Path.Combine(vsBuildOutputDirectory, "Cmdty.Storage.Excel-AddIn64.xll"), x64Folder);
    CopyFiles(System.IO.Path.Combine(vsBuildOutputDirectory, "Cmdty.Storage.Excel-AddIn64.dna"), x64Folder);


});

Task("Pack-Excel")
	.IsDependentOn("Test-C#")
    .Does(setupContext =>
{
    Information("Creating x86 Excel add-in zip file.");
    CreateAddInZipFile("x86", artifactsDirectory.ToString(), vsBuildOutputDirectory);
    Information("x86 Excel add-in zip file has been created.");

    Information("Creating x64 Excel add-in zip file.");
    CreateAddInZipFile("x64", artifactsDirectory.ToString(), vsBuildOutputDirectory);
    Information("x64 Excel add-in zip file has been created.");
});

private void WriteFileToZip(ZipArchive zipArchive, string filePath, string zipEntryName)
{
    byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
    ZipArchiveEntry entry = zipArchive.CreateEntry(zipEntryName);
    using (Stream entryStream = entry.Open())
        entryStream.Write(fileBytes);
}

private void CreateAddInZipFile(string platform, string artifactsDirectory, string vsBuildOutputDirectory)
{
    string xllName = $"Cmdty.Storage-{platform}.xll";
    string zipFileName = $"Cmdty.Storage-{platform}.zip";
    string addInZipFilePath = System.IO.Path.Combine(artifactsDirectory, zipFileName);
    string vsPublishDirectory = System.IO.Path.Combine(vsBuildOutputDirectory, "publish");
    using (System.IO.FileStream fileStream = System.IO.File.Create(addInZipFilePath))
    using (ZipArchive zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create))
    {
        WriteFileToZip(zipArchive, System.IO.Path.Combine(vsBuildOutputDirectory, "Cmdty.Storage.dll"), "Cmdty.Storage.dll");
        WriteFileToZip(zipArchive, System.IO.Path.Combine(vsBuildOutputDirectory, "Cmdty.Storage.pdb"), "Cmdty.Storage.pdb");
        WriteFileToZip(zipArchive, System.IO.Path.Combine(vsBuildOutputDirectory, platform, "libiomp5md.dll"), "libiomp5md.dll");
        WriteFileToZip(zipArchive, System.IO.Path.Combine(vsBuildOutputDirectory, platform, "MathNet.Numerics.MKL.dll"), "MathNet.Numerics.MKL.dll");
        WriteFileToZip(zipArchive, System.IO.Path.Combine(vsPublishDirectory, xllName), xllName);
    }
}

private string GetEnvironmentVariable(string envVariableName)
{
    string envVariableValue = EnvironmentVariable(envVariableName);
    if (string.IsNullOrEmpty(envVariableValue))
        throw new ApplicationException($"Environment variable '{envVariableName}' has not been set.");
    return envVariableValue;
}

private void StartProcessThrowOnError(string applicationName, params string[] processArgs)
{
    var argsBuilder = new ProcessArgumentBuilder();
    foreach(string processArg in processArgs)
    {
        argsBuilder.Append(processArg);
    }
    int exitCode = StartProcess(applicationName, new ProcessSettings {Arguments = argsBuilder});
    if (exitCode != 0)
        throw new ApplicationException($"Starting {applicationName} in new process returned non-zero exit code of {exitCode}");
}

private void RunCommandInVirtualEnv(string command, string vEnvActivatePath)
{
    Information("Running command in venv: " + command);
    string fullCommand = isWindows ? $"/k {vEnvActivatePath} & {command} & deactivate & exit" :
                    $"-c {vEnvActivatePath} && {command} && deactivate && exit";
    Information($"Command to execute: {shellCommand} " + fullCommand);
    StartProcessThrowOnError(shellCommand, fullCommand);
}

var publishNuGetTask = Task("Publish-NuGet")
    .Does(() =>
{
    string nugetApiKey = GetEnvironmentVariable("NUGET_API_KEY");

    var nupkgPath = GetFiles(artifactsDirectory.ToString() + "/*.nupkg").Single();

    NuGetPush(nupkgPath, new NuGetPushSettings 
    {
        ApiKey = nugetApiKey,
        Source = "https://api.nuget.org/v3/index.json"
    });
});

var publishTestPyPiTask = Task("Publish-TestPyPI")
    .Does(() =>
{
    string testPyPiPassword = GetEnvironmentVariable("TEST_PYPI_PASSWORD");
    StartProcessThrowOnError(pythonCommand, "-m twine upload --repository-url https://test.pypi.org/legacy/ src/Cmdty.Storage.Python/dist/*",
                                        "--username fowja", "--password " + testPyPiPassword);
});

var publishPyPiTask = Task("Publish-PyPI")
    .Does(() =>
{
    string pyPiPassword = GetEnvironmentVariable("PYPI_PASSWORD");
    StartProcessThrowOnError(pythonCommand, "-m twine upload src/Cmdty.Storage.Python/dist/*",
                                        "--username __token__", "--password " + pyPiPassword);
});

if (!publishWithoutBuild)
{
    publishTestPyPiTask.IsDependentOn("Pack-Python");
    publishPyPiTask.IsDependentOn("Pack-Python");
    publishNuGetTask.IsDependentOn("Pack-NuGet");
}
else
{
    Information("Publishing without first building as PublishWithoutBuild variable set to true.");
}

var defaultTask = Task("Default");

defaultTask
	.IsDependentOn("Pack-NuGet")
    .IsDependentOn("Pack-Python");

if (isWindows)
    defaultTask.IsDependentOn("Pack-Excel");

RunTarget(target);
