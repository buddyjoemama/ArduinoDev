using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CompileHelper
{
    class Program
    {
        /// <summary>
        /// Usage: CompileHelper -b=C:\somepath\ -o=C:\somepath\bin -s=\files\mymain.cpp -l=C:\somelib\
        /// -p = Project file (full path)
        /// -b = Base source directory
        /// -o = Ouput directory (can be relative)
        /// -l = Library folder (can be more than 1 library specified. Can be relative to the main base source directory)
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            if (args.Count() < 3)
            {
                Console.WriteLine(@"Usage: CompileHelper -b=C:\somepath\ -o=C:\somepath\bin -s=\files\mymain.cpp");
                return;
            }

            String projectDirectory = null;
            String baseOutputDir = null;
            String mainSourceFile = null;
            String projectFile = null;
            List<String> libraries = new List<string>();
            List<String> links = new List<string>();

            foreach(String arg in args)
            {
                String[] argVal = arg.Split(new char[] {'='});
                if(argVal[0].Contains("-d"))
                {
                    projectDirectory = argVal[1];
                }
                else if(argVal[0].Contains("-o"))
                {
                    baseOutputDir = argVal[1];
                }
                else if (argVal[0].Contains("-l"))
                {
                    libraries.Add(argVal[1]);
                }
                else if(argVal[0].Contains("-p"))
                {
                    projectFile = argVal[1];
                }
            }

            // Process included libraries from project file
            libraries.AddRange(ProcessProjectFileIncludes(projectFile));

            // Fix relative paths.
            if(!Path.IsPathRooted(baseOutputDir))
                baseOutputDir = Path.Combine(projectDirectory, baseOutputDir);

            // Main source file is <project name>.cpp
            if (mainSourceFile == null)
                mainSourceFile = String.Concat(Path.GetFileNameWithoutExtension(projectFile), ".cpp");

            if(!Path.IsPathRooted(mainSourceFile))
                mainSourceFile = Path.Combine(projectDirectory, mainSourceFile);

            for (int i = 0; i < libraries.Count(); i++)
            {
                if (!Path.IsPathRooted(libraries[i]))
                    libraries[i] = Path.Combine(projectDirectory, libraries[i]);
            }

            // Main source file is relative to the base source directory. Concat the paths.
            mainSourceFile = Path.Combine(projectDirectory, mainSourceFile);

            Console.WriteLine("Executing with the following parameters");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("Base source directory: {0}", projectDirectory);
            Console.WriteLine("Output directory: {0}", baseOutputDir);
            Console.WriteLine("Main source file: {0}\n", mainSourceFile);

            String[] allCFiles = Directory.EnumerateFiles(projectDirectory, "*.c", SearchOption.AllDirectories).ToArray();
            String[] allCPPFiles = Directory.EnumerateFiles(projectDirectory, "*.cpp", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".cpp")).ToArray();

            // Base cpp complile command
            String baseCPPCompileCommand = @"-c -g -Os -Wall -fno-exceptions -ffunction-sections -mmcu=atmega32u4 -DF_CPU=16000000L -MMD -DUSB_VID=0x2341 -DUSB_PID=0x8036 -DARDUINO=101"; // Base compilation string -I{0} {1} -o {2}.o";

            // Base C compile command
            String baseCCompileCommand = @"-c -g -Os -Wall -ffunction-sections -fdata-sections -mmcu=atmega32u4 -DF_CPU=16000000L -MMD -DUSB_VID=0x2341 -DUSB_PID=0x8036 -DARDUINO=101"; // Base compilation string 

            // Base link command (to avr-gcc)
            String baseLinkCommand = @"-Os -Wl,--gc-sections -mmcu=atmega32u4 -o";

            // Combine all libraries
            String libraryIncludes = String.Join(" -I", libraries.ToArray());

            // Empty output directory
            Directory.EnumerateFiles(baseOutputDir).ToList().ForEach(s => File.Delete(s));

            // Create output directory.
            Directory.CreateDirectory(baseOutputDir);

            Compile("avr-gcc", libraryIncludes, baseOutputDir, baseCCompileCommand, allCFiles);
            Compile("avr-g++", libraryIncludes, baseOutputDir, baseCPPCompileCommand, allCPPFiles);

            // Combine binaries into a single library. Exclude the main source file (arduino entry point) for the project.
            var image = CombineBinaries(mainSourceFile, baseOutputDir);

            // Link against the newly created .a file (alon
            string elfFile = Link(projectDirectory, projectFile, mainSourceFile, baseLinkCommand, image, baseOutputDir);

            // Run post build events.
            RunPostBuildEvent(projectFile);

            ObjectCopy(mainSourceFile, elfFile, baseOutputDir);
        }

        /// <summary>
        /// Copy the elf file to a hex file, which can be uploaded to the device.
        /// </summary>
        /// <param name="elfFile"></param>
        private static void ObjectCopy(String mainSourceFile, String elfFile, String outputDir)
        {
            Console.WriteLine("\nCopying to eep/hex files");

            String outputFile = Path.Combine(outputDir,
                String.Concat(Path.GetFileName(mainSourceFile), ".hex"));

            String command = String.Format("-O ihex -R .eeprom {0} {1}", elfFile, outputFile);

            ProcessStartInfo info = new ProcessStartInfo("avr-objcopy", command);
            info.RedirectStandardOutput = true;
            info.RedirectStandardInput = true;
            info.UseShellExecute = false;

            var process = Process.Start(info);
            var stream = process.StandardOutput;

            var outString = stream.ReadToEnd();

            if (!String.IsNullOrWhiteSpace(outString))
                Console.WriteLine(outString);

            process.WaitForExit();

            Console.WriteLine("Wrote {0}\n", outputFile);
        }

        /// <summary>
        /// Run the post build process specified in the project file.
        /// </summary>
        /// <param name="projectFile"></param>
        private static void RunPostBuildEvent(String projectFile)
        {
            var root = XElement.Load(projectFile);

            string command = (from e in root.Descendants()
                              where e.Name.LocalName == "PostBuildEvent"
                              select e.Value).FirstOrDefault();

            if (!String.IsNullOrWhiteSpace(command))
            {
                Console.WriteLine("\nExecuting post build command: {0}", command);

                ProcessStartInfo info = new ProcessStartInfo("cmd", "/C " + command);
                info.WorkingDirectory = Path.GetDirectoryName(projectFile);
                info.RedirectStandardOutput = true;
                info.RedirectStandardInput = true;
                info.UseShellExecute = false;
               
                var process = Process.Start(info);
                var stream = process.StandardOutput;

                var outString = stream.ReadToEnd();

                if (!String.IsNullOrWhiteSpace(outString))
                    Console.WriteLine(outString);

                process.WaitForExit();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectDir"></param>
        /// <param name="mainSourceFile"></param>
        /// <param name="baseLinkCommand"></param>
        /// <param name="combinedOutputImagePath"></param>
        /// <param name="baseOutputDir"></param>
        private static String Link(String projectDir, String projectFile, String mainSourceFile, String baseLinkCommand, 
            String combinedOutputImagePath, String baseOutputDir)
        {
            // Scan the project file for external libraries (archives) being referenced.
            var archivePaths = from e in XElement.Load(projectFile).Descendants()
                               .Where(e => e.Name.LocalName == "avrgcccpp.linker.libraries.LibrarySearchPaths")
                           from v in e.Descendants()
                           where v.Name.LocalName == "Value"
                           select v.Value;
                          
            // Scan the paths for acceptable archive files (.a)
            var archiveFiles = (from p in archivePaths
                                let fixedPath = (Path.IsPathRooted(p) ? Path.Combine(projectDir, p) : projectDir)
                                select Directory.EnumerateFiles(fixedPath, "*.a")).SelectMany(s => s);

            var archives = String.Join(" ", archiveFiles.ToArray());

            // Name of the output elf file.
            String elfOut = Path.Combine(baseOutputDir, Path.GetFileNameWithoutExtension(mainSourceFile));

            // Final output named from project file.
            string args = String.Format("{3} {0}.elf {0}.o {1} -L{2} -lm",
                elfOut,
                String.Join(" ", archives, 
                    File.Exists(combinedOutputImagePath) ? combinedOutputImagePath : ""),
                baseOutputDir,
                baseLinkCommand);

            Console.WriteLine("Linking with the following options: {0}", args);

            ProcessStartInfo info = new ProcessStartInfo("avr-gcc", args);
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;

            var process = Process.Start(info);
            var stream = process.StandardOutput;

            var outString = stream.ReadToEnd();

            if (!String.IsNullOrWhiteSpace(outString))
                Console.WriteLine(outString);

            process.WaitForExit();

            return String.Concat(elfOut, ".elf");
        }

        /// <summary>
        /// Combines all target binaries into a single 'core.a', which will
        /// be linked in later. Excludes the main arduino entry point file.
        /// </summary>
        /// <param name="mainSourceFile">Main source file for project.</param>
        /// <param name="outputDir">Target output directory.</param>
        private static String CombineBinaries(String mainSourceFile, String outputDir)
        {
            String[] files = Directory.EnumerateFiles(outputDir, "*.o")
                                .Where(s => !Path.GetFileNameWithoutExtension(s).Contains(Path.GetFileNameWithoutExtension(mainSourceFile)))
                                .ToArray();

            if(files.Length > 0)
                Console.WriteLine("\nCombining binaries...\n");

            String outputImageName = String.Format("{0}.a", Path.Combine(outputDir, Path.GetFileNameWithoutExtension(mainSourceFile)));

            foreach (string file in files)
            {
                Console.WriteLine("Adding file {0}", file);

                // Final output named from project file.
                string args = String.Format("rcs {0} {1}",
                    outputImageName,
                    file);

                ProcessStartInfo info = new ProcessStartInfo("avr-ar", args);
                info.RedirectStandardOutput = true;
                info.UseShellExecute = false;

                var process = Process.Start(info);
                var stream = process.StandardOutput;

                var outString = stream.ReadToEnd();

                if (!String.IsNullOrWhiteSpace(outString))
                    Console.WriteLine(outString);

                process.WaitForExit();
            }

            return outputImageName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="command"></param>
        /// <param name="files"></param>
        private static void Compile(String executable, String libraryIncludes, String baseOutputDir, String command, String[] files)
        {
            // Gather and compile each cpp file.
            foreach (String cFile in files)
            {
                Console.WriteLine("Compiling {0}", cFile);

                // First compile all C files.
                string compArgs = String.Concat(command,
                    String.Format(" -I{0} ", libraryIncludes),
                    cFile,
                    String.Format(" -o {0}.o", Path.Combine(baseOutputDir, Path.GetFileNameWithoutExtension(cFile))));

                ProcessStartInfo info = new ProcessStartInfo(executable, compArgs);
                info.RedirectStandardOutput = true;
                info.UseShellExecute = false;

                var process = Process.Start(info);
                var stream = process.StandardOutput;

                var outString = stream.ReadToEnd();

                if (!String.IsNullOrWhiteSpace(outString))
                    Console.WriteLine(outString);

                process.WaitForExit();
            }
        }

        /// <summary>
        /// Pull the libraries from the project file.
        /// </summary>
        /// <param name="projectFile"></param>
        private static List<String> ProcessProjectFileIncludes(String projectFile)
        {
            var root = XElement.Load(projectFile);

            return (from path in root.Descendants().Where(s => s.Name.LocalName == "avrgcc.compiler.directories.IncludePaths")
                    from include in path.Descendants()
                    where include.Name.LocalName == "Value"
                    select include.Value)
                .Union(
                    (from path in root.Descendants().Where(s => s.Name.LocalName == "avrgcccpp.compiler.directories.IncludePaths")
                     from include in path.Descendants()
                     where include.Name.LocalName == "Value"
                     select include.Value)).Distinct().ToList();
        }
    }
}
