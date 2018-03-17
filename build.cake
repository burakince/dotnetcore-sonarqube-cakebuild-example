#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=2.4.5"
#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool&version=4.1.0"
#tool "nuget:?package=coveralls.net&version=0.7.0"
#addin "nuget:?package=Cake.Sonar&version=1.0.2"
#addin "nuget:?package=Cake.Coveralls&version=0.7.0"
#tool "nuget:?package=DependencyCheck.Runner.Tool&include=./**/dependency-check.sh&include=./**/dependency-check.bat"
#addin "nuget:?package=Cake.DependencyCheck"

var target = Argument("target", "Default");
var sonarToken = EnvironmentVariable("SONAR_TOKEN") ?? "abcdef0123456789";
var buildVersion = EnvironmentVariable("APPVEYOR_BUILD_VERSION") ?? "1.0";
var coverallsToken = EnvironmentVariable("COVERALLS_REPO_TOKEN") ?? "abcdef0123456789";
var configuration = Context.Argument("configuration", "Release");

var rootDir = (DirectoryPath)Context.Directory(".");
var artifacts = rootDir.Combine(".artifacts");
var testResults = artifacts.Combine("Test-Results");
var checkResults = artifacts.Combine("Check-Results");

var objDirectories = GetDirectories("./**/**/obj/*");
var binDirectories = GetDirectories("./**/**/bin/*");

var projectName = "CustomerService";
var scanPath = "src/Customer/*";
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

    if (!DirectoryExists(checkResults))
    {
        CreateDirectory(checkResults);
    }
});

Task("Clean")
    .Does(() => 
    {
        CleanDirectories(objDirectories);
        CleanDirectories(binDirectories);
        CleanDirectories(testResults.ToString());
        CleanDirectories(checkResults.ToString());
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

Task("Upload-Coverage-Report")
    .Does(() => {
        CoverallsNet(testCoverageOutput, CoverallsNetReportType.OpenCover, new CoverallsNetSettings()
        {
            RepoToken = coverallsToken
        });
    });

Task("Dependency-Check")
    .Does(() =>
    {
        DependencyCheck(new DependencyCheckSettings
        {
            Project = projectName,
            Scan = scanPath,
            Out = checkResults.ToString(),
            Format = "XML"
        });
    });

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Tests");

Task("Sonar-Analysis")
    .IsDependentOn("SonarBegin")
    .IsDependentOn("Default")
    .IsDependentOn("SonarEnd");

Task("Appveyor")
    .IsDependentOn("Sonar-Analysis")
    .IsDependentOn("Upload-Coverage-Report");

Task("Generate-Report")
    .IsDependentOn("Default")
    .IsDependentOn("Report");

Task("Security")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Build")
    .IsDependentOn("Dependency-Check");

RunTarget(target);
