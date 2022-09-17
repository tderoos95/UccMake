using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;
using UnrealUniverse.UccMake;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
    .CreateLogger();

try
{
    Compile();
}
catch (Exception ex)
{
    Log.Fatal(ex, "An error occurred in {UccMake} while compiling:", "UccMake.exe");
}

void Compile()
{
    string workspaceDirectory = Environment.CurrentDirectory;

    // If running from IDE set workSpaceDirectory to "C:\Desktop\"
    if (Debugger.IsAttached)
    {
        workspaceDirectory = @"D:\UT2004\Wormhole";
    }

    var workspaceName = Path.GetFileName(workspaceDirectory);

    var uccPath = Path.GetDirectoryName(workspaceDirectory);
    var systemDirectory = Path.Combine(uccPath, Constants.File.SystemDirectory);
    uccPath = Path.Combine(systemDirectory, Constants.File.Ucc);

    if (!File.Exists(uccPath))
    {
        Log.Error("{UccPath} not found", uccPath);
        return;
    }

    string configurationPath = Path.Combine(workspaceDirectory, Constants.File.MakeIni);

    if (!File.Exists(configurationPath))
    {
        Log.Error("{ConfigurationPath} not found", configurationPath);
        return;
    }

    var compiledFilePath = Path.Combine(systemDirectory, $"{workspaceName}.u");
    var backupCompiledFilePath = Path.Combine(systemDirectory, $"{workspaceName}.u.bak");

    // Create a backup of the old compiled file, then delete the old compiled file
    if (File.Exists(compiledFilePath))
    {
        if (File.Exists(backupCompiledFilePath))
            File.Delete(backupCompiledFilePath);

        File.Copy(compiledFilePath, backupCompiledFilePath, true);
        Log.Information("Created backup of {CompiledFilePath}", compiledFilePath);
        Log.Information("Backup saved as {BackupCompiledFilePath}", backupCompiledFilePath);

        File.Delete(compiledFilePath);
    }

    var compileProcess = new Process();
    compileProcess.StartInfo.UseShellExecute = false;
    compileProcess.StartInfo.RedirectStandardOutput = true;
    compileProcess.StartInfo.FileName = uccPath;
    compileProcess.StartInfo.Arguments = $"make -ini={configurationPath}";
    compileProcess.Start();

    while (!compileProcess.StandardOutput.EndOfStream)
    {
        var line = compileProcess.StandardOutput.ReadLine();
        FormatAndLog(line);
    }

    compileProcess.WaitForExit();
}

void FormatAndLog(string line)
{
    if (line.Contains(Constants.Compiler.WarningMessage) || line.Contains(Constants.Compiler.ErrorMessage))
    {
        var split = line.Split(" : ");
        var className = split[0].Trim();
        var message = split[1].Trim();

        if (line.Contains(Constants.Compiler.WarningMessage))
            Log.Warning("{ClassName} : " + message, className);
        else if (line.Contains(Constants.Compiler.ErrorMessage))
            Log.Error("{ClassName} : " + message, className);

        return;
    }

    if (line == Constants.Compiler.CompileAbortedMessage)
    {
        Log.Error(line);
        return;
    }

    if (line.StartsWith(Constants.Compiler.CompileFailureMessagePrefix) ||
        line.StartsWith(Constants.Compiler.CompileSuccessMessagePrefix))
    {
        // Extract number of errors and warnings in string "Failure - 1 error(s), 1 warning(s)"
        var split = line.Split(" - ")[1].Split(", ");
        var errors = split[0].Split(" ")[0];
        var warnings = split[1].Split(" ")[0];

        if (line.StartsWith(Constants.Compiler.CompileFailureMessagePrefix))
            Log.Error("{Errors} error(s), {Warnings} warning(s)", errors, warnings);
        else if (line.StartsWith(Constants.Compiler.CompileSuccessMessagePrefix))
            Log.Information("{Errors} error(s), {Warnings} warning(s)", errors, warnings);
   
        return;
    }

    Log.Information(line);
}