#tool nuget:?package=Codecov
#addin nuget:?package=Cake.Codecov
#tool "nuget:?package=OpenCover"
#tool "nuget:?package=NUnit.ConsoleRunner"

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
var tests = $"./output/{project}.Tests.dll";
var publishPath = MakeAbsolute(Directory("./output"));
var codedovToken = EnvironmentVariable("CODECOV_TOKEN");

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
                ArgumentCustomization = arg => arg.AppendSwitch("/p:DebugType","=","Full"),
				NoRestore = false,
				Configuration = configuration
			});
	});

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {

       // Information(MakeAbsolute(Directory(tests)));

        OpenCover(tool => {

                

                tool.NUnit3(tests);
            },
            new FilePath("./TestResult.xml"),
            new OpenCoverSettings()
                .WithFilter($"+[{project}]*")
                .WithFilter($"-[{project}.Tests]*"));
        });

Task("Upload-Coverage")
    .IsDependentOn("Test")
    .Does(() =>
{
    // Upload a coverage report by providing the Codecov upload token.
    Codecov("coverage.xml", codedovToken);
});

Task("Publish")
    .IsDependentOn("Build")
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
    .IsDependentOn("Publish")
    .IsDependentOn("Upload-Coverage");

RunTarget(target);