using static System.Console;
using static ConsoleLog;

using static FileEnumerator;
using static FileAnalyzer;
using static FileSynchronizer;

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
            WriteLine("    CodeSync Analyze <RutaOrigen> <RutaDestino> [<Opciones>]");
            WriteLine("    CodeSync Sync <SyncXml>");
            WriteLine();

            return;
        }

        var command = args[0].ToLowerInvariant();

        if (command == "analyze")
        {
            if (!ReadArgumentsForAnalyze(args.AsSpan(1), out var options))
                return;

            WriteLine($"Analizando coincidencias...");
            WriteLine();

            Analyze(options);
        }
        else if (command == "sync")
        {
            if (!ReadArgumentsForSynchronize(args.AsSpan(1), out var options))
                return;

            WriteLine($"Leyendo archivo XML `{Path.GetFileName(options.InputXml)}`...");
            WriteLine();

            Synchronize(options);
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
    static bool ReadArgumentsForAnalyze(ReadOnlySpan<string> args, out FileAnalyzerOptions options)
    {
        options = default;

        if (args.Length == 0)
        {
            WriteLine("  Uso:");
            WriteLine("    CodeSync Analyze <RutaOrigen> <RutaDestino> [<Opciones>]");

            WriteLine();
            WriteLine("""
                    CodeSync analizará los archivos en el directorio especificado por
                    <RutaOrigen> y todos sus subdirectorios, y también los archivos en
                    <RutaDestino> y todos sus subdirectorios, generando un resumen donde
                    especifica la ruta original de un archivo y la ruta de destino en el
                    repositorio de destino, así como cualquier posible incidencia.

                    Opciones:
                      --output <ArchivoXml>
                            -o <ArchivoXml>

                            Genera un archivo XML donde se especifica la ruta original
                            de un archivo y la ruta en el repositorio de destino donde
                            se debe copiar.

                            Dicho archivo se puede usar más tarde con el comando Sync
                            para sincronizar dos repositorios.

                            Los archivos que no tengan coincidencia directa en los dos
                            repositorios se marcarán para que el usuario pueda especificar
                            si son archivos renombrados, nuevos, etc.

                      --hash
                          -h

                            Para determinar mejor la coincidencia de archivos en origen
                            y destino, compara los contenidos de los archivos mediante
                            una firma hash.
                """);

            WriteLine();
            return false;
        }

        if (args.Length < 2)
        {
            LogError("El comando Analyze debe incluir las rutas de los repositorios de origen y destino.");
            return false;
        }

        var sourcePath = Path.GetFullPath(args[0]);
        if (Directory.Exists(sourcePath) == false)
        {
            LogError("El directorio de origen especificado no existe.");
            return false;
        }
        var destPath = Path.GetFullPath(args[1]);
        if (Directory.Exists(destPath) == false)
        {
            LogError("El directorio de destino especificado no existe.");
            return false;
        }

        args = args[2..];

        string? outputXmlPath = null;
        bool matchByHash = false;

        while (args.Length > 0)
        {
            var arg = args[0].ToLowerInvariant();

            switch (arg)
            {
                case "-o":
                case "--output":
                {
                    if (args.Length < 2)
                    {
                        LogError("No se ha especificado el nombre del archivo XML.");
                        return false;
                    }
                    outputXmlPath = args[1];

                    if (Path.GetExtension(outputXmlPath).ToLowerInvariant() is not ".xml" or "")
                        outputXmlPath += ".xml";

                    if (File.Exists(outputXmlPath))
                        LogWarning("El archivo XML de destino ya existe. Va a ser sobreescrito.");

                    args = args[2..];
                    break;
                }
                case "-h":
                case "--hash":
                {
                    matchByHash = true;
                    args = args[1..];
                    break;
                }
                default:
                {
                    LogError($"""
                        No se reconocen algunos de los comandos:

                        {arg}
                        """);
                    return false;
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

        options = new FileAnalyzerOptions
        {
            SourceDirectory = sourcePath,
            SourceFilesQueue = sourceFileQueue,

            DestinationDirectory = destPath,
            DestinationFilesMap = destFileMap,

            UseHashMatching = matchByHash,

            OutputXmlFilePath = outputXmlPath
        };
        return true;
    }

    //
    // Reads the command line arguments for the Sync command, and validates them.
    //
    static bool ReadArgumentsForSynchronize(ReadOnlySpan<string> args, out FileSynchronizerOptions options)
    {
        options = default;

        if (args.Length == 0)
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

            WriteLine();
            return false;
        }

        var inputXml = Path.GetFullPath(args[0]);
        if (File.Exists(inputXml) == false)
        {
            LogError("El archivo XML especificado no existe.");
            return false;
        }

        options = new FileSynchronizerOptions { InputXml = inputXml };
        return true;
    }
}
