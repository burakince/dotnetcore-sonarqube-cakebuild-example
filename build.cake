#tool "nuget:https://www.nuget.org/api/v2?package=OpenCover&version=4.6.519"
#tool "nuget:https://www.nuget.org/api/v2?package=ReportGenerator&version=2.4.5"
#tool "nuget:https://www.nuget.org/api/v2?package=MSBuild.SonarQube.Runner.Tool&version=4.0.2"
#addin "nuget:https://www.nuget.org/api/v2?package=Cake.Sonar&version=1.0.2"

var target = Argument("target", "Default");
var sonarToken = EnvironmentVariable("SONAR_TOKEN") ?? "abcdef0123456789";
var buildVersion = EnvironmentVariable("APPVEYOR_BUILD_VERSION") ?? "1.0";
var configuration = Context.Argument("configuration", "Release");

var rootDir = (DirectoryPath)Context.Directory(".");
var artifacts = rootDir.Combine(".artifacts");
var testResults = artifacts.Combine("Test-Results");

var objDirectories = GetDirectories("./**/**/obj/*");
var binDirectories = GetDirectories("./**/**/bin/*");

var solution = rootDir.CombineWithFilePath("CustomerService.sln");
var testCoverageOutput = testResults.CombineWithFilePath("OpenCover.xml");

Setup(context =>
{
    if (!DirectoryExists(artifacts))
    {
        CreateDirectory(artifacts);
    }

    if (!DirectoryExists(testResults))
    {
        CreateDirectory(testResults);
    }
});

Task("Clean")
    .Does(() => 
    {
        CleanDirectories(objDirectories);
        CleanDirectories(binDirectories);
        CleanDirectories(testResults.ToString());
    });

Task("Restore-NuGet-Packages")
    .Does(() =>
    {
        DotNetCoreRestore(solution.ToString());
    });

Task("SonarBegin")
    .Does(() => {
        SonarBegin(new SonarBeginSettings{
            Url = "https://sonarcloud.io",
            Key = "CustomerService",
            Name = "Customer Service",
            Login = sonarToken,
            OpenCoverReportsPath = testCoverageOutput.ToString(),
            Organization = "burakince-github",
            Version = buildVersion
        });
    });

Task("Build")
    .Does(() =>
    {
        DotNetCoreBuild(solution.ToString(),
        new DotNetCoreBuildSettings
        {
            Configuration = configuration,
            ArgumentCustomization = arg => arg.AppendSwitch("/p:DebugType","=","Full")
        });
    });

Task("Run-Tests")
    .Does(() =>
    {
        var success = true;
        var openCoverSettings = new OpenCoverSettings
        {
            OldStyle = true,
            MergeOutput = true
        }
        .WithFilter("+[*]* -[*.Test*]* -[Moq]*");

        var testProjects = GetFiles("./test/**/*.csproj");
        foreach (var project in testProjects)
        {
            try 
            {
                var projectFile = MakeAbsolute(project).ToString();
                var dotNetTestSettings = new DotNetCoreTestSettings
                {
                    Configuration = configuration,
                    NoBuild = true
                };

                OpenCover(context => context.DotNetCoreTest(projectFile, dotNetTestSettings), testCoverageOutput, openCoverSettings);
            }
            catch (Exception ex)
            {
                success = false;
                Error("There was an error while running the tests", ex);
            }
        }

        if (success == false)
        {
            throw new CakeException("There was an error while running the tests");
        }
    });

Task("Report")
    .Does(() => 
    {
        ReportGenerator(testCoverageOutput, testResults);
    });

Task("SonarEnd")
    .Does(() => {
        SonarEnd(new SonarEndSettings{
            Login = sonarToken
        });
    });

Task("Build-and-Test")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Tests");

Task("Sonar-Analysis")
    .IsDependentOn("SonarBegin")
    .IsDependentOn("Build-and-Test")
    .IsDependentOn("SonarEnd");

Task("Generate-Report")
    .IsDependentOn("Build-and-Test")
    .IsDependentOn("Report");

Task("Default")
    .IsDependentOn("Build-and-Test");

RunTarget(target);
