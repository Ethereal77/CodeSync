using static System.Console;

static class ConsoleLog
{
    private static readonly ConsoleColor ColorError = ConsoleColor.Red;
    private static readonly ConsoleColor ColorWarning = ConsoleColor.Yellow;

    private static void WriteColored(string text, ConsoleColor color)
    {
        var prevColor = ForegroundColor;
        ForegroundColor = color;
        Write(text);
        ForegroundColor = prevColor;
    }
    private static void WriteLineColored(string text, ConsoleColor color)
    {
        var prevColor = ForegroundColor;
        ForegroundColor = color;
        WriteLine(text);
        ForegroundColor = prevColor;
    }

    public static void LogError(string errorMessage)
    {
        WriteLineColored($"ERROR: {errorMessage}", ColorError);
        WriteLine();
    }

    public static void LogWarning(string warningMessage)
    {
        WriteLineColored($"ATENCIÓN: {warningMessage}", ColorWarning);
        WriteLine();
    }

    public static void LogWarning(string warningMessage, string file)
    {
        WriteLineColored($"""
                          ATENCIÓN: {warningMessage}
                                    {file}
                          """, ColorWarning);
        WriteLine();
    }

    public static void LogWarningAmbiguous(string file, IEnumerable<string> possibleMatches)
    {
        var prevColor = ForegroundColor;
        ForegroundColor = ColorWarning;

        WriteLine($"""
                   ATENCIÓN: El archivo no tiene una coincidencia clara en el destino.
                             {file}

                             Las posibles opciones son:

                   """);

        ForegroundColor = ConsoleColor.DarkYellow;

        foreach (var possibleMatch in possibleMatches)
            WriteLine($"          {possibleMatch}");

        WriteLine();
        ForegroundColor = prevColor;
    }

    public static void LogMatch(string fileName, string sourcePath, string destPath, bool hashMatch)
    {
        WriteLineColored(fileName, hashMatch ? ConsoleColor.Cyan : ConsoleColor.Green);

        if (hashMatch)
            WriteLineColored($"  ⚠️ Se determinó la coincidencia comparando el contenido de los archivos.", ConsoleColor.DarkGray);

        WriteColored($"  Origen: ", ConsoleColor.DarkGray);
        WriteLine(sourcePath);
        WriteColored($"  Destino: ", ConsoleColor.DarkGray);
        WriteLine(destPath);

        WriteLine();
    }

    public static void LogWarningFileMapFilesRemaining(FileMap fileMap)
    {
        var prevColor = ForegroundColor;
        ForegroundColor = ColorWarning;
        
        WriteLine("""
            ATENCIÓN: Los siguientes archivos del repositorio de destino no tienen una coincidencia
                      clara en el repositorio de origen:

            """);
        
        foreach (var (destFileName, destFileSource) in fileMap)
        {
            if (destFileSource is SingleFileSource singleFile)
                WriteLine(singleFile.FilePath);

            else if (destFileSource is MultipleFileSource multiFile)
                foreach (var fileName in multiFile)
                    WriteLine(fileName);
        }
        WriteLine();
        
        ForegroundColor = prevColor;
    }

    public static void LogStatistics(int filesMatched, int filesMatchedByHash,
                                     int filesInSourceNotInDest,
                                     int filesInSourceMultiInDest,
                                     int filesInDestNotInSource)
    {
        WriteLine($"Coincidencias entre origen y destino: {filesMatched}");
        
        if (filesMatchedByHash > 0)
            WriteLine($"  De las cuales han sido decididas comparando el contenido: {filesMatchedByHash}");

        if (filesInSourceNotInDest > 0)
            WriteLine($"Archivos sin coincidencia en destino: {filesInSourceNotInDest}");
        if (filesInSourceMultiInDest > 0)
            WriteLine($"Archivos con más de una coincidencia en destino: {filesInSourceMultiInDest}");

        if (filesInDestNotInSource > 0)
            WriteLine($"Archivos en destino sin coincidencia en origen: {filesInDestNotInSource}");

        WriteLine();
    }
}
