using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;
using UnrealUniverse.UccMake;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()    
    .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
    .CreateLogger();

var workspaceDirectory = Environment.CurrentDirectory;

#region Flatten commandlet

if (args.Length > 0 && args[0] == "--flattensource")
{
    FlattenCommandlet();
    return;
}


void FlattenCommandlet()
{
    var sourceCodeFolder = args[1];
    var sourceCodePath = Path.Combine(workspaceDirectory, sourceCodeFolder);
    var sourceCodeDirectory = new DirectoryInfo(sourceCodePath);

    if(!sourceCodeDirectory.Exists)
    {
        Log.Error("Source code directory {Path} does not exist.", sourceCodePath);
        Log.Error("Flattening aborted.");
        return;
    }

    var compilerInputPath = Path.Combine(workspaceDirectory, "classes");
    var compilerInputDirectory = new DirectoryInfo(compilerInputPath);

    Log.Information("Source code directory: {Path}", sourceCodePath);
    Log.Information("Files from subdirectories will be copied to: {Path}", compilerInputPath);

    if(!compilerInputDirectory.Exists)
    {
        Directory.CreateDirectory(compilerInputPath);
    }
    else if(compilerInputDirectory.GetFiles().Any())
    {
        Log.Error("Classes directory is not empty, make sure to empty it before flattening the source code.");
        Log.Error("Flattening aborted.");
        return;
    }

    // Flatten the source code
    var totalAmountOfFiles = 0;
    var flattenedFileCount = 0;
    FlattenDirectory(sourceCodeDirectory, compilerInputPath, ref totalAmountOfFiles, ref flattenedFileCount);

    // Show results
    if (flattenedFileCount != totalAmountOfFiles)
    {
        Log.Warning("Flattened {FlattenedFileCount} out of {TotalFiles} files", flattenedFileCount, totalAmountOfFiles);
        Log.Warning("Flattening finished with warning(s):");
    }
    else
    {
        Log.Information("Flattened {FlattenedFileCount} out of {TotalFiles} files.", flattenedFileCount, totalAmountOfFiles);
        Log.Information("Flattening finished successfully.");
    }
}

void FlattenDirectory(DirectoryInfo directory, string compilerInputPath, ref int totalAmount, ref int flattenedFileCount)
{
    var files = directory.GetFiles();
    totalAmount += files.Length;

    foreach (var file in files)
    {
        var destinationPath = Path.Combine(compilerInputPath, file.Name);
        if (TryMoveFile(file, destinationPath))
            flattenedFileCount++;
    }

    foreach (var subDirectory in directory.GetDirectories())
        FlattenDirectory(subDirectory, compilerInputPath, ref totalAmount, ref flattenedFileCount);
}

bool TryMoveFile(FileInfo file, string destinationPath)
{
    try
    {
        file.CopyTo(destinationPath);
        return true;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "An error occurred while copying {File} to {DestinationPath}", file.Name, destinationPath);
        return false;
    }
}

#endregion

#region Default flow

try
{
    PreBuild();
    Compile();
}
catch (Exception ex)
{
    Log.Fatal(ex, "An error occurred in {UccMake} while compiling:", "UccMake.exe");
}

#endregion

#region PreBuild logic

void PreBuild()
{
    var workspaceDirectory = Environment.CurrentDirectory;
    var preBuildPath = Path.Combine(workspaceDirectory, Constants.File.PreBuild);

    Log.Information("Executing {PreBuildPath}..", preBuildPath);

    // run prebuild.bat and redirect output to console
    if (File.Exists(preBuildPath))
    {
        var preBuildProcess = new Process();
        preBuildProcess.StartInfo.FileName = preBuildPath;
        preBuildProcess.Start();
        preBuildProcess.WaitForExit();
    }
}

#endregion

#region Compile logic

void Compile()
{
    // If running from IDE set workSpaceDirectory to a specific UT2004 project directory
    if (Debugger.IsAttached)
    {
        workspaceDirectory = @"D:\UT2004\RandomArena";
    }

    var workspaceName = Path.GetFileName(workspaceDirectory);
    var utDirectory = Path.GetDirectoryName(workspaceDirectory);
    ArgumentNullException.ThrowIfNull(utDirectory);

    var systemDirectory = Path.Combine(utDirectory, Constants.File.SystemDirectory);
    var uccPath = Path.Combine(systemDirectory, Constants.File.Ucc);

    if (!File.Exists(uccPath))
    {
        Log.Error("{UccPath} not found", uccPath);
        return;
    }

    var configurationPath = Path.Combine(workspaceDirectory, Constants.File.MakeIni);

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
        ArgumentNullException.ThrowIfNull(line);

        FormatAndLog(line);
    }

    compileProcess.WaitForExit();

    if (compileProcess.ExitCode == 0)
        PostBuild();
}

void FormatAndLog(string line)
{
    #region Compiler formatting
    if (line.Contains(Constants.Compiler.WarningMessage) || line.Contains(Constants.Compiler.ErrorMessage))
    {
        var split = line.Split(" : ");
        var className = split[0].Trim();
        var message = string.Join(" : ", values: split.Skip(1)).Trim();

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

    #endregion

    #region PostBuild formatting
    if (line.StartsWith(Constants.PostBuild.CopyingPrefix))
    {
        var fileName = line.Split(" ")[1];
        var formattedLine = line.Replace(fileName, "{FileName}");
        Log.Information(formattedLine, fileName);
        return;
    }

    if(line.EndsWith(Constants.PostBuild.CopiedSuffix))
    {
        var amountOfFiles = line.Trim().Split(" ")[0];
        var formattedLine = line.Replace(amountOfFiles, "{AmountOfFiles}");
        Log.Information(formattedLine, amountOfFiles);
        return;
    }

    #endregion

    Log.Information(line);
}

#endregion

#region PostBuild logic

void PostBuild()
{
    var postBuildPath = Path.Combine(workspaceDirectory, Constants.File.PostBuild);

    if (File.Exists(postBuildPath))
    {
        Log.Information("Executing {PostBuildPath}..", postBuildPath);

        var postBuildProcess = new Process();
        postBuildProcess.StartInfo.UseShellExecute = false;
        postBuildProcess.StartInfo.RedirectStandardOutput = true;
        postBuildProcess.StartInfo.FileName = postBuildPath;
        postBuildProcess.Start();

        while (!postBuildProcess.StandardOutput.EndOfStream)
        {
            var line = postBuildProcess.StandardOutput.ReadLine();
            ArgumentNullException.ThrowIfNull(line);
            FormatAndLog(line);
        }

        postBuildProcess.WaitForExit();
    }
}

#endregion