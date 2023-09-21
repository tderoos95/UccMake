namespace UnrealUniverse.UccMake;

public static class Constants
{
    public static class File
    {
        public const string Ucc = "ucc.exe";
        public const string SystemDirectory = "System";
        public const string MakeIni = "make.ini";
        public const string PreBuild = "PreBuild.bat";
        public const string PostBuild = "PostBuild.bat";
    }

    public static class Compiler
    {
        public const string CompileSuccessMessagePrefix = "Success - ";
        public const string CompileFailureMessagePrefix = "Failure - ";
        public const string CompileAbortedMessage = "Compile aborted due to errors.";
        public const string ErrorMessage = ": Error,";
        public const string WarningMessage = ": Warning,";
    }

    public static class PostBuild
    {
        public const string CopyingPrefix = "Copying ";
        public const string CopiedSuffix = " copied.";
    }
}
