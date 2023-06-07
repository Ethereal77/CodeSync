namespace CodeSync;

using CodeSync.Utils;

using static System.Console;
using static CodeSync.Utils.ConsoleLog;
using static CodeSync.Utils.FileEnumerator;

using static FileAnalyzer;
using static FileSynchronizer;
using static FileUpdater;
using static FileVerifier;

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
            WriteLine("    CodeSync Update <SyncXml> [<Opciones>]");
            WriteLine("    CodeSync Verify <SyncXml> [<Opciones>]");
            WriteLine("    CodeSync Sync <SyncXml>");
            WriteLine();

            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "analyze":
            {
                if (!ReadArgumentsForAnalyze(args.AsSpan(1), out var options))
                    return;

                WriteLine($"Analizando coincidencias...");
                WriteLine();

                Analyze(options);
                break;
            }
            case "sync":
            {
                if (!ReadArgumentsForSynchronize(args.AsSpan(1), out var options))
                    return;

                WriteLine($"Leyendo archivo XML `{Path.GetFileName(options.InputXml)}`...");
                WriteLine();

                Synchronize(options);
                break;
            }
            case "update":
            {
                if (!ReadArgumentsForUpdate(args.AsSpan(1), out var options))
                    return;

                WriteLine($"Leyendo archivo XML `{Path.GetFileName(options.InputXml)}`...");
                WriteLine();

                Update(options);
                break;
            }
            case "verify":
            {
                if (!ReadArgumentsForVerify(args.AsSpan(1), out var options))
                    return;

                WriteLine($"Leyendo archivo XML `{Path.GetFileName(options.InputXml)}`...");
                WriteLine();

                Verify(options);
                break;
            }

            default:
                LogError($"Comando no reconocido `{args[0]}`.");
                WriteLine();
                break;
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
                            del repositorio de origen y la ruta del repositorio de destino
                            donde se debe copiar.

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

        // Analyze the source directory, converting to relative paths, and applying exclusion rules
        WriteLine($"Buscando en el directorio de origen `{sourcePath}`...");

        var sourceFiles = EnumerateFiles(sourcePath);
        var sourceFileQueue = new Queue<string>(sourceFiles);

        WriteLine($"Se han encontrado {sourceFileQueue.Count} archivos.");
        WriteLine();

        // Analyze the destination directory, converting to relative paths, and applying exclusion rules
        WriteLine($"Buscando en el directorio de destino `{destPath}`...");

        var destFiles = EnumerateFiles(destPath);
        var destFileMap = new FileMap(destFiles);

        WriteLine($"Se han encontrado {destFileMap.Count} archivos.");
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

    //
    // Reads the command line arguments for the Update command, and validates them.
    //
    static bool ReadArgumentsForUpdate(ReadOnlySpan<string> args, out FileUpdaterOptions options)
    {
        options = default;

        if (args.Length == 0)
        {
            WriteLine("  Uso:");
            WriteLine("    CodeSync Update <SyncXml> [<Opciones>]");

            WriteLine();
            WriteLine("""
                CodeSync analizará el archivo de resumen <SyncXml> previamente generado
                por el comando Analyze y lo actualizará.

                Se recorrerán sus entradas y se descartarán aquellas que hagan referencia
                a archivos o directorios que ya no existen.
                A continuación se volverán a analizar los directorios de origen y destino
                para verificar los cambios que hayan podido ocurrir en ambos.

                Opciones:
                  --output <ArchivoXml>
                        -o <ArchivoXml>

                        Se genera un nuevo archivo XML actualizado donde se especifica
                        la ruta original del repositorio de origen y la ruta del repositorio
                        de destino donde se debe copiar.

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

                  --discard-older
                               -d

                        Se descartarán aquellos archivos en el repositorio de origen que
                        sean más antiguos que la fecha de modificación del archivo XML o
                        que la fecha de modificación del archivo correspondiente en el
                        repositorio de destino.
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

        args = args[1..];

        string? outputXmlPath = null;
        bool matchByHash = false;
        bool discardOlder = false;

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
                case "-d":
                case "--discard-older":
                {
                    discardOlder = true;
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

        options = new FileUpdaterOptions
        {
            InputXml = inputXml,

            OutputXmlFilePath = outputXmlPath,

            UseHashMatching = matchByHash,
            DiscardOldFiles = discardOlder
        };
        return true;
    }

    //
    // Reads the command line arguments for the Verify command, and validates them.
    //
    static bool ReadArgumentsForVerify(ReadOnlySpan<string> args, out FileVerifierOptions options)
    {
        options = default;

        if (args.Length == 0)
        {
            WriteLine("  Uso:");
            WriteLine("    CodeSync Verify <SyncXml> [<Opciones>]");

            WriteLine();
            WriteLine("""
                    CodeSync analizará el archivo de resumen <SyncXml> previamente generado
                    por el comando Analyze y verificará que las entradas son correctas, que
                    no están repetidas, que los archivos a los que hace referencia existen, etc.

                    Opciones:
                        --output <ArchivoXml>
                              -o <ArchivoXml>

                                Se genera un nuevo archivo XML con las entradas que han pasado
                                la verificación, ordenadas.

                                Dicho archivo se puede usar más tarde con el comando Sync
                                para sincronizar dos repositorios.

                        --check-repeats
                                    -cr

                                Se comprobará si existen entradas de archivos a copiar o de
                                archivos a ignorar repetidas.

                        --check-existing
                                     -ce

                                Se comprobará si los archivos a los que hacen referencia las
                                entradas de copia o de ignorar existen físicamente.
                                Esta opción implica `-cec` y `-cei`.

                        --check-existing-copy
                                         -cec

                                Se comprobará si los archivos a los que hacen referencia las
                                entradas de copia existen físicamente.

                        --check-existing-ignore
                                           -cei

                                Se comprobará si los archivos a los que hacen referencia las
                                entradas de ignorar existen físicamente.
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

        args = args[1..];

        string? outputXmlPath = null;
        bool checkRepeats = false;
        var checkExisting = CheckExistingEntryOption.None;

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
                case "-cr":
                case "--check-repeats":
                {
                    checkRepeats = true;
                    args = args[1..];
                    break;
                }
                case "-ce":
                case "--check-existing":
                {
                    checkExisting = CheckExistingEntryOption.CheckAll;
                    args = args[1..];
                    break;
                }
                case "-cec":
                case "--check-existing-copy":
                {
                    checkExisting = CheckExistingEntryOption.CheckCopyEntries;
                    args = args[1..];
                    break;
                }
                case "-cei":
                case "--check-existing-ignore":
                {
                    checkExisting = CheckExistingEntryOption.CheckIgnoreEntries;
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

        options = new FileVerifierOptions
        {
            InputXml = inputXml,

            OutputXmlFilePath = outputXmlPath,

            DiscardRepeatedEntries = checkRepeats,
            DiscardMissingFiles = checkExisting
        };
        return true;
    }
}
