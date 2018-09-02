///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PARAMETERS
//////////////////////////////////////////////////////////////////////

var project = "VCDiff.Core";
var projectCli = $"{project}.Cli";
var solution = $"./{project}.sln";
var tests = $"./{project}.Tests/{project}.Tests.csproj";
var publishPath = MakeAbsolute(Directory("./output"));

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does( () => {
        CleanDirectories($"./{project}/obj/**/*.*");
        CleanDirectories($"./{project}/bin/{configuration}/**/*.*");
		CleanDirectories($"./{projectCli}/obj/**/*.*");
        CleanDirectories($"./{projectCli}/bin/{configuration}/**/*.*");
});

Task("Clean-Publish")
    .IsDependentOn("Clean")
    .Does( () => {
        CleanDirectory(publishPath);
});

Task("Build")
    .Does(() => 
	{
		DotNetCoreBuild(solution,
			new DotNetCoreBuildSettings 
			{
				NoRestore = true,
				Configuration = configuration
			});

		DotNetCoreBuild(solution,
			new DotNetCoreBuildSettings 
			{
				NoRestore = true,
				Configuration = configuration
			});
	});

Task("Test")
    .IsDependentOn("Build")
    .Does( () => {
    DotNetCoreTest(tests,
        new DotNetCoreTestSettings {
            NoBuild = true,
            NoRestore = true,
            Configuration = configuration
        });
});

Task("Publish")
    .IsDependentOn("Test")
    .IsDependentOn("Clean-Publish")
    .Does( () => {
    DotNetCorePublish(solution,
        new DotNetCorePublishSettings {

            NoRestore = true,
            Configuration = configuration,
            OutputDirectory = publishPath
        });
});

Task("Default")
    .IsDependentOn("Test");

Task("AppVeyor")
    .IsDependentOn("Publish");

RunTarget(target);