using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Components;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using static Nuke.Common.IO.FileSystemTasks;

namespace UnrealUniverse.UccMake;

class Build : NukeBuild, IGlobalTool
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter] readonly AbsolutePath SourceDirectory = EnvironmentInfo.WorkingDirectory;

    [PathExecutable("ucc")] readonly Tool Ucc;

    public Build()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
            .CreateLogger();
    }

    Target Compile => _ => _
        .DependsOn(Backup)
        .Executes(() =>
        {
            var systemDirectory = SourceDirectory / Constants.File.SystemDirectory;

            var ucc = Ucc ?? ToolResolver.GetLocalTool(systemDirectory / "ucc.exe");

            var configurationPath = SourceDirectory / Constants.File.MakeIni;

            ucc($"make -ini={configurationPath}");
        });

    Target Backup => _ => _
        .Executes(() =>
        {
            var systemDirectory = SourceDirectory / Constants.File.SystemDirectory;

            var compiledFilePath = systemDirectory / $"{SourceDirectory.Name}.u";
            var backupCompiledFilePath = systemDirectory / $"{SourceDirectory.Name}.u.bak";

            if (compiledFilePath.FileExists())
            {
                if (backupCompiledFilePath.FileExists())
                    DeleteFile(backupCompiledFilePath);

                CopyFile(compiledFilePath, backupCompiledFilePath);
                Log.Information("Created backup of {CompiledFilePath}", compiledFilePath);
                Log.Information("Backup saved as {BackupCompiledFilePath}", backupCompiledFilePath);

                DeleteFile(compiledFilePath);
            }
        });
}
