#tool "nuget:?package=Cake.CoreCLR";
#tool "nuget:?package=xunit.runner.console"
#tool "nuget:?package=NuGet.CommandLine"

#addin "Cake.FileHelpers"
#addin "nuget:?package=NuGet.Core"
#addin "nuget:?package=Cake.ExtendedNuGet"

using NuGet;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var projectName = "Stove.EntityFramework.Migrator";
var solution = "./" + projectName + ".sln";

var appveyorBranch = EnvironmentVariable("APPVEYOR_REPO_BRANCH");
var nugetApiKey = EnvironmentVariable("nugetApiKey");

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var toolpath = Argument("toolpath", @"tools");
var branch = Argument("branch", appveyorBranch);

var targetTestFramework = "net452";
var testFileRegex = $"**/bin/{configuration}/{targetTestFramework}/*Tests*.dll";
var testProjectNames = new List<string>()
                      {
						 //"Stove.Migrator.Tests.Domain"                          
                      };

var nupkgPath = "nupkg";
var nupkgRegex = $"**/Stove*.nupkg";
var nugetPath = toolpath + "/nuget.exe";
var nugetQueryUrl = "https://www.nuget.org/api/v2/";
var nugetPushUrl = "https://www.nuget.org/api/v2/package";
var NUGET_PUSH_SETTINGS = new NuGetPushSettings
                          {
                              ToolPath = File(nugetPath),
                              Source = nugetPushUrl,
                              ApiKey = nugetApiKey
                          };

var isDeployableBranch = branch == "master";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        Information("Current Branch is:" + branch);
        CleanDirectories("./src/**/bin");
        CleanDirectories("./src/**/obj");
        CleanDirectory(nupkgPath);
    });

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetCoreRestore(solution);
    });

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
    {
        DotNetBuild(solution, c=> c.Configuration = configuration);
    });

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
    {
        foreach(var testProject in testProjectNames)
        {
           var testFile = GetFiles($"**/bin/{configuration}/{targetTestFramework}/{testProject}*.dll").First();
           Information(testFile);
           XUnit2(testFile.ToString(), new XUnit2Settings { });
        }
    });

Task("Coverage")
    .IsDependentOn("Run-Unit-Tests")
    .Does(()=>
    {
      Information("Coverage...");
    });

Task("Analyse")
    .IsDependentOn("Coverage")
    .Does(()=>
    {
        Information("Sonar running!...");
    });

Task("Pack")
    .IsDependentOn("Analyse")
    .Does(() =>
    {
        var nupkgFiles = GetFiles(nupkgRegex);
        MoveFiles(nupkgFiles, nupkgPath);
    });

Task("NugetPublish")
    .IsDependentOn("Pack")
    .WithCriteria(() => isDeployableBranch)
    .Does(()=>
    {
        foreach(var nupkgFile in GetFiles(nupkgRegex))
        {
          if(!IsNuGetPublished(nupkgFile, nugetQueryUrl))
          {
             Information("Publishing... " + nupkgFile);
             NuGetPush(nupkgFile, NUGET_PUSH_SETTINGS);
          }
          else
          {
             Information("Already published, skipping... " + nupkgFile);
          }
        }
    });

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Pack")
    .IsDependentOn("NugetPublish");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);