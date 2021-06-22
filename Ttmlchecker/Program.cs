using System;
using System.IO;
using System.Linq;
using CommandLine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ttmlchecker
{
    class Program
    {
        class Options
        {
            [Option('i', "input", Required = true,
              HelpText = "Input path to the folder where locale subfolders are.")]
            public string InputPath { get; set; }

            [Option('l', "length", Required = true,
              HelpText = "Maximum caption length for single line caption. For English 48, for others 70")]
            public Int32 CaptionLength { get; set; }

            [Option('c', "count", Default = false,
              HelpText = "Compare the number of files between localized and English.")]
            public bool CountFiles { get; set; }
        }
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
             .WithParsed<Options>(opts => ParseDir(opts))
             .WithNotParsed((errs) => HandleParseError(errs));
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            Console.WriteLine("Command Line parameters provided were not valid!");
            //throw new NotImplementedException();
        }

        private static void ParseDir(Options opts)
        {
            if (!string.IsNullOrEmpty(opts.InputPath))
            {

                if (File.Exists(opts.InputPath))
                {
                    ProcessFile(opts.InputPath, opts.CaptionLength); // If path is a file
                }
                else if (Directory.Exists(opts.InputPath))
                {
                    if (opts.CountFiles)
                    {
                        CountFiles(opts.InputPath);
                    }
                    ProcessDirectory(opts.InputPath, opts.CaptionLength);  // If path is a directory
                }
                else
                {
                    Console.WriteLine($"{0} is not a valid file or directory.", opts.InputPath);
                }
            }
        }
        public static void CountFiles(string filespath)
        {
            // Put all file names in directory into an array. 
            string lang1 = "en-us";
            string[] lang2 = { "da-dk", "de-de", "es-es", "fi-fi", "fr-fr", "it-it", "ja-jp", "nb-no", "nl-nl", "pt-br", "sv-se" };
            foreach (string s in lang2)
            {
                List<string> files1 = new List<string>();
                List<string> files2 = new List<string>();
                string pattern = @"_[a-z]{2}-[a-z]{2}";
                string firstPath = Path.Combine(filespath, lang1);
                string secondPath = Path.Combine(filespath, s);
                if (Directory.Exists(firstPath) && (Directory.Exists(secondPath)))
                {
                    string[] array1 = Directory.GetFiles(firstPath, "*.ttml").Select(file => Path.GetFileName(file)).ToArray();
                    foreach (string name in array1)
                    {
                        string modifiedName = Regex.Replace(name, pattern, "", RegexOptions.IgnoreCase);
                        files1.Add(modifiedName);
                    }
                    string[] array2 = Directory.GetFiles(secondPath, "*.ttml").Select(file => Path.GetFileName(file)).ToArray();
                    foreach (string name in array2)
                    {
                        string modifiedName = Regex.Replace(name, pattern, "", RegexOptions.IgnoreCase);
                        files2.Add(modifiedName);
                    }
                }
                var firstNotSecond = files1.Except(files2).ToList();
                var secondNotFirst = files2.Except(files1).ToList();
                if (firstNotSecond.Any())
                {
                    Console.WriteLine($"Missing files in {s}:");
                    firstNotSecond.ForEach(Console.WriteLine);
                }
                if (secondNotFirst.Any())
                {
                    Console.WriteLine($"Extra files in {s}:");
                    secondNotFirst.ForEach(Console.WriteLine);
                }
                else
                {
                    Console.WriteLine($"Path to {s} does not exist.");
                }
            }
        }

        public static void ProcessDirectory(string targetDirectory, int length)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory, "*.ttml");
            foreach (string filePath in fileEntries)
            {
                ProcessFile(filePath, length);
            }
            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
                if (!subdirectory.Contains(".")) // to avoid recursing system folders
                {
                    ProcessDirectory(subdirectory, length);
                }
        }
        // Check the caption length and duration is according to requirements
        public static void ProcessFile(string filePath, int length)
        {
            Regex filename_lang = new Regex(@"([a-z]{2}-[a-z]{2})(\.ttml)");
            Regex file_lang = new Regex(@"[_\""]([a-z]{2}-[a-z]{2})([^a-zA-Z])");
            Regex regex_time = new Regex(@"(\d{2}\:\d{2}\:\d{2}\.\d{2,3}). end=.(\d{2}\:\d{2}\:\d{2}\.\d{2,3})");
            Regex find_html = new Regex(@"(&[^\s]*;)");
            Regex regex_caption = new Regex(@".*'>(.*)</p>");
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                int singleLineCaptionLength = length; //the max length of a single line caption
                int captionMaxLength = 2 * singleLineCaptionLength; //the max length of a caption
                int captionDuration = 2; //the minimum duration for a caption in seconds
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match_lang = file_lang.Match(line);
                    Match match_file = filename_lang.Match(filePath);
                    if (match_lang.Success && (match_lang.Groups[1].ToString() != match_file.Groups[1].ToString()) && lineNumber < 5)
                    {
                        Console.WriteLine(filePath);
                        Console.WriteLine("The language definition in the filename does not match the language definition in the file.");
                        Console.WriteLine(line);
                    }
                    Match match_time = regex_time.Match(line);
                    if (match_time.Success)
                    {
                        DateTime timeIn = DateTime.Parse(match_time.Groups[1].Value);
                        var timeInOnly = timeIn - timeIn.Date;
                        DateTime timeOut = DateTime.Parse(match_time.Groups[2].Value);
                        var timeOutOnly = timeOut - timeOut.Date;
                        //var previousTimeOut = timeOutOnly;
                        TimeSpan difference = timeOutOnly.Subtract(timeInOnly);
                        if (difference.TotalSeconds < captionDuration)
                        {
                            Console.WriteLine(filePath);
                            Console.WriteLine($"The duration for caption on line {lineNumber} is too short, it is only {difference.TotalSeconds} seconds.");
                            Console.WriteLine(line);
                        }
                    }
                    Match match_caption = regex_caption.Match(line);
                    if (match_caption.Success)
                    {
                        string ttmlCaption = match_caption.Groups[1].Value;
                        string caption = ttmlCaption.Replace("<br />", " ");
                        if (ttmlCaption.Length > singleLineCaptionLength && (!ttmlCaption.Contains("<br />")))
                        {
                            Console.WriteLine(filePath);
                            Console.WriteLine($"The caption on line {lineNumber} should be on two lines");
                            Console.WriteLine(line);
                        }
                        else if (caption.Length > captionMaxLength)
                        {
                            Console.WriteLine(filePath);
                            Console.WriteLine($"The caption on line {lineNumber} is too long it is {caption.Length} characters long");
                            Console.WriteLine(caption);
                        }
                        Match match_html = find_html.Match(line);
                        if (match_html.Success)
                        {
                            Console.WriteLine(filePath);
                            Console.WriteLine($"The caption on line {lineNumber} has html entities");
                            Console.WriteLine(line);
                        }
                    }
                    lineNumber++;
                }
            }
        }

    }
}
