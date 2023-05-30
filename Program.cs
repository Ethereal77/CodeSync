using static System.Console;
using static ConsoleLog;

using static FileEnumerator;
using static FileAnalyzer;

internal class Program
{
    private static void Main(string[] args)
    {
        WriteLine("CodeSync - Sincroniza los archivos de un repositorio en otro");
        WriteLine("© Infinisis 2023");

        WriteLine();

        if (args.Length == 0)
        {
            WriteLine("  Uso:");
            WriteLine("    CodeSync Analyze <RutaOrigen> <RutaDestino> [<RutaSyncXml>]");
            WriteLine("    CodeSync Sync <SyncXml>");
            WriteLine();

            return;
        }

        var command = args[0].ToLowerInvariant();

        if (command == "analyze")
        {
            var options = ReadArgumentsForAnalyze(args);
            if (options.SourceDirectory is null)
                return;

            WriteLine($"Analizando coincidencias...");
            WriteLine();

            Analyze(options);
        }
        else if (command == "sync")
        {
            var syncXmlPath = Path.GetFullPath(args[1]);
            if (File.Exists(syncXmlPath) == false)
            {
                LogError("El archivo de resumen XML especificado no existe.");
                return;
            }
        }
        else
        {
            LogError($"Comando no reconocido `{args[0]}`.");
            WriteLine();
        }
    }

    //
    // Reads the command line arguments for the Analyze command, and validates them.
    //
    static FileAnalyzerOptions ReadArgumentsForAnalyze(string[] args)
    {
        if (args.Length == 1)
        {
            WriteLine("  Uso:");
            WriteLine("    CodeSync Analyze <RutaOrigen> <RutaDestino> [<RutaSyncXml>]");

            WriteLine();
            WriteLine("""
                    CodeSync analizará los archivos en el directorio especificado por
                    <RutaOrigen> y todos sus subdirectorios, y también los archivos en
                    <RutaDestino> y todos sus subdirectorios, generando un archivo de
                    resumen XML donde se especifica la ruta original de un archivo y la
                    ruta de destino en el repositorio de destino.

                    Dicho archivo XML se puede usar más tarde con el comando Sync para
                    sincronizar dos repositorios.

                    Los archivos que no tengan coincidencia directa en los dos repositorios
                    se marcarán para que el usuario pueda especificar si son archivos
                    renombrados, nuevos, etc.
                """);
            
            WriteLine();
            return default;
        }

        if (args.Length < 3)
        {
            LogError("El comando Analyze debe incluir las rutas de los repositorios de origen y destino.");
            return default;
        }

        var sourcePath = Path.GetFullPath(args[1]);
        if (Directory.Exists(sourcePath) == false)
        {
            LogError("El directorio de origen especificado no existe.");
            return default;
        }
        var destPath = Path.GetFullPath(args[2]);
        if (Directory.Exists(destPath) == false)
        {
            LogError("El directorio de destino especificado no existe.");
            return default;
        }

        string? outputXmlPath = null;
        bool matchByHash = false;

        for (int argIndex = 3; argIndex < args.Length; argIndex++)
        {
            var arg = args[argIndex].ToLowerInvariant();

            switch (arg)
            {
                case "-o":
                case "--output":
                {
                    if (args.Length <= argIndex + 1)
                    {
                        LogError("No se ha especificado el nombre del archivo XML.");
                        return default;
                    }
                    outputXmlPath = args[++argIndex];

                    if (Path.GetExtension(outputXmlPath).ToLowerInvariant() is not ".xml" or "")
                        outputXmlPath += ".xml";

                    if (File.Exists(outputXmlPath))
                        LogWarning("El archivo XML de destino ya existe. Va a ser sobreescrito.");

                    break;
                }
                case "-h":
                case "--hash":
                {
                    matchByHash = true;
                    break;
                }
                default:
                {
                    LogError($"""
                        No se reconocen algunos de los comandos:

                        {arg}
                        """);
                    return default;
                }
            }
        }

        // Analiza el directorio de origen, convirtiendo a rutas relativas, y aplicando exclusiones
        WriteLine($"Buscando en el directorio de origen `{sourcePath}`...");

        var sourceFiles = EnumerateFiles(sourcePath);
        var sourceFileQueue = new Queue<string>(sourceFiles);

        WriteLine($"Se han encontrado {sourceFileQueue.Count} archivos...");
        WriteLine();

        // Analiza el directorio de destino, convirtiendo a rutas relativas, y aplicando exclusiones
        WriteLine($"Buscando en el directorio de destino `{destPath}`...");

        var destFiles = EnumerateFiles(destPath);
        var destFileMap = new FileMap(destFiles);

        WriteLine($"Se han encontrado {destFileMap.Count} archivos...");
        WriteLine();

        return new FileAnalyzerOptions
        {
            SourceDirectory = sourcePath,
            SourceFilesQueue = sourceFileQueue,

            DestinationDirectory = destPath,
            DestinationFilesMap = destFileMap,

            UseHashMatching = matchByHash,

            OutputXmlFilePath = outputXmlPath
        };
    }

    //
    // Reads the command line arguments for the Sync command, and validates them.
    //
    static FileAnalyzerOptions ReadArgumentsForSynchronize(string[] args)
    {
        if (args.Length == 1)
        {
            WriteLine("  Uso:");
            WriteLine("    CodeSync Sync <SyncXml>");

            WriteLine();
            WriteLine("""
                    CodeSync analizará el archivo de resumen <SyncXml> previamente generado
                    por el comando Analyze y comenzará la sincronización.

                    Se copiarán los archivos del repositorio de origen en el repositorio de
                    destino, sobreescribiendo si fuera necesario, renombrando en aquellos
                    casos en los que sea adecuado, según lo dispuesto por el archivo XML.
                """);
        }

        WriteLine();
        return default;
    }
}
