using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace Roblox_Legacy_Place_Convertor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string fileToConvertPath;
        private string newFilePath;
        private string inputFolderPath;
        private string outputFolderPath;
        private bool isConverting;
        private ConversionMode conversionMode = ConversionMode.SingleFile;
        private static readonly HashSet<string> supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".rbxlx",
            ".rbxmx",
            ".rbxl",
            ".rbxm"
        };
        private static readonly Dictionary<string, string> outputExtensionByInput = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".rbxlx", ".rbxl" },
            { ".rbxl", ".rbxl" },
            { ".rbxmx", ".rbxm" },
            { ".rbxm", ".rbxm" }
        };
        private static readonly Dictionary<string, string> color3uint8ToBrickColor = new Dictionary<string, string> // Yes, i wrote this all manually. Took me about 2 hours.
        {
            {"4294112243", "1"},
            {"4294901760", "1004"},
            {"4288986439", "119"},
            {"4294298928", "24"},
            {"4292511041", "106"},
            {"4291045404", "21"},
            {"4285215356", "104"},
            {"4279069100", "23"},
            {"4278226844", "107"},
            {"4283144011", "37"},
            {"4294506744", "1001"},
            {"4293256415", "208"},
            {"4291677645", "1002"},
            {"4288914085", "194"},
            {"4284702562", "199"},
            {"4279970357", "26"},
            {"4279308561", "1003"},
            {"4286549604", "1022"},
            {"4293040960", "105"},
            {"4293572754", "125"},
            {"4287986039", "153"},
            {"4287388575", "1023"},
            {"4285826717", "135"},
            {"4285438410", "102"},
            {"4286091394", "151"},
            {"4292330906", "5"},
            {"4294830733", "226"},
            {"4294946560", "1017"},
            {"4292511354", "101"},
            {"4293442248", "9"},
            {"4286626779", "11"},
            {"4279430868", "1018"},
            {"4288791692", "29"},
            {"4294954137", "1030"},
            {"4294967244", "1029"},
            {"4294953417", "1025"},
            {"4294928076", "1016"},
            {"4289832959", "1026"},
            {"4289715711", "1024"},
            {"4288672745", "1027"},
            {"4291624908", "1028"},
            {"4290887234", "1008"},
            {"4294967040", "1009"},
            {"4294901951", "1032"},
            {"4278190335", "1010"},
            {"4278255615", "1019"},
            {"4278255360", "1020"},
            {"4286340166", "217"},
            {"4291595881", "18"},
            {"4288700213", "38"},
            {"4284622289", "1031"},
            {"4290019583", "1006"},
            {"4278497260", "1013"},
            {"4290040548", "45"},
            {"4282023189", "1021"},
            {"4285087784", "192"},
            {"4289352960", "1014"},
            {"4288891723", "1007"},
            {"4289331370", "1015"},
            {"4280374457", "1012"},
            {"4278198368", "1011"},
            {"4280844103", "28"},
            {"4280763949", "141"}
        };
        private enum ConversionMode
        {
            SingleFile,
            BatchFolder
        }

        private sealed class ConversionOptions
        {
            public bool IncludeColors { get; set; }
            public bool IncludeUnionData { get; set; }
            public bool ConvertScripts { get; set; }
            public bool ConvertFolders { get; set; }
            public bool ChangeRbxassetid { get; set; }
            public bool ConvertTextSize { get; set; }
        }

        private sealed class BatchProgress
        {
            public int Processed { get; set; }
            public int Total { get; set; }
            public string CurrentFile { get; set; }
            public bool IsCompleted { get; set; }
        }

        private sealed class BatchSummary
        {
            public int TotalDiscovered { get; set; }
            public int Converted { get; set; }
            public int Skipped { get; set; }
            public int Failed { get; set; }
            public List<string> Errors { get; } = new List<string>();
        }
        public MainWindow()
        {
            InitializeComponent();
            UpdateModeUi();
        }

        private void GithubHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // You can remove this if you want, this is just me self promoting lol
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConverting == true) // So you can't browse for a place while it's converting
            {
                return;
            }
            // Opens a file dialog asking you which file do you want to convert
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Roblox XML Place Files (*.rbxlx)|*.rbxlx|Roblox XML Model Files (*.rbxmx)|*.rbxmx|Roblox XML Place Files (*.rbxl)|*.rbxl|Roblox XML Model Files (*.rbxm)|*.rbxm";
            if (fileDialog.ShowDialog() == true)
            {
                fileToConvertPath = fileDialog.FileName;
                PlaceSelectedLabel.Content = "File selected: " + fileToConvertPath;
            }
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConverting == true)
            {
                return;
            }

            using (var folderDialog = new Forms.FolderBrowserDialog())
            {
                folderDialog.Description = "Select the input folder";
                folderDialog.SelectedPath = inputFolderPath ?? string.Empty;
                if (folderDialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    inputFolderPath = folderDialog.SelectedPath;
                    InputFolderLabel.Content = "Input folder: " + inputFolderPath;
                }
            }
        }

        private void BrowseOutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConverting == true)
            {
                return;
            }

            using (var folderDialog = new Forms.FolderBrowserDialog())
            {
                folderDialog.Description = "Select the output folder";
                folderDialog.SelectedPath = outputFolderPath ?? string.Empty;
                if (folderDialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    outputFolderPath = folderDialog.SelectedPath;
                    OutputFolderLabel.Content = "Output folder: " + outputFolderPath;
                }
            }
        }

        private void ModeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            conversionMode = SingleFileRadioButton.IsChecked == true ? ConversionMode.SingleFile : ConversionMode.BatchFolder;
            UpdateModeUi();
        }

        private async void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConverting == true) // If you somehow managed to click the Convert button twice
            {
                return;
            }

            if (conversionMode == ConversionMode.SingleFile)
            {
                ConvertSingleFile();
                return;
            }

            await ConvertBatchAsync();
        }

        private void ConvertSingleFile()
        {
            if (string.IsNullOrWhiteSpace(fileToConvertPath)) // When you haven't selected a place file
            {
                MessageBox.Show("Please select a model or place you'd like to convert by clicking on 'Browse'", "Cannot convert place", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string fileContents;
            string readError;
            if (!TryReadInputFile(fileToConvertPath, out fileContents, out readError))
            {
                MessageBox.Show(readError, "Cannot convert place", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.Filter = "Roblox XML Place Files (*.rbxl)|*.rbxl|Roblox XML Model Files (*.rbxm)|*.rbxm";
            if (fileDialog.ShowDialog() != true)
            {
                return;
            }

            newFilePath = fileDialog.FileName;
            ProgressBar.Value = 0;
            ProgressLabel.Content = "";
            SetConvertingState(true);

            bool converted;
            string writeError;
            try
            {
                converted = TryWriteConvertedFile(fileContents, newFilePath, GetConversionOptions(), out writeError);
            }
            finally
            {
                SetConvertingState(false);
            }

            if (!converted)
            {
                MessageBox.Show(writeError, "Cannot convert place", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ProgressBar.Value = 100;
            ProgressLabel.Content = "Done!";
            MessageBox.Show("Conversion done!", "Conversion status", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ConvertBatchAsync()
        {
            if (string.IsNullOrWhiteSpace(inputFolderPath) || string.IsNullOrWhiteSpace(outputFolderPath))
            {
                MessageBox.Show("Please select both an input folder and an output folder.", "Cannot convert folder", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(inputFolderPath))
            {
                MessageBox.Show("Input folder does not exist.", "Cannot convert folder", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (IsSubdirectory(inputFolderPath, outputFolderPath))
            {
                MessageBox.Show("Output folder cannot be inside the input folder.", "Cannot convert folder", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            List<string> files = DiscoverSupportedFiles(inputFolderPath);
            if (files.Count == 0)
            {
                MessageBox.Show("No supported Roblox files were found in the selected folder.", "Nothing to convert", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Directory.CreateDirectory(outputFolderPath);

            ProgressBar.Value = 0;
            ProgressLabel.Content = "";
            SetConvertingState(true);

            var summary = new BatchSummary { TotalDiscovered = files.Count };
            var progress = new Progress<BatchProgress>(UpdateBatchProgress);
            var options = GetConversionOptions();

            try
            {
                await Task.Run(() => ConvertBatchFiles(files, inputFolderPath, outputFolderPath, options, summary, progress));
            }
            catch (Exception ex)
            {
                summary.Failed++;
                summary.Errors.Add("Batch error: " + ex.Message);
            }
            finally
            {
                SetConvertingState(false);
            }

            ProgressBar.Value = 100;
            ProgressLabel.Content = "Done!";
            ShowBatchSummary(summary);
        }

        private void UpdateBatchProgress(BatchProgress progress)
        {
            if (progress == null || progress.Total == 0)
            {
                ProgressBar.Value = 0;
                return;
            }

            double percent = (double)progress.Processed / progress.Total * 100;
            ProgressBar.Value = percent;

            if (progress.IsCompleted)
            {
                ProgressLabel.Content = "Converted " + progress.Processed + "/" + progress.Total + ": " + progress.CurrentFile;
            }
            else
            {
                ProgressLabel.Content = "Converting " + (progress.Processed + 1) + "/" + progress.Total + ": " + progress.CurrentFile;
            }
        }

        private void ConvertBatchFiles(List<string> files, string inputRoot, string outputRoot, ConversionOptions options, BatchSummary summary, IProgress<BatchProgress> progress)
        {
            var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int processed = 0;

            foreach (string file in files)
            {
                string relativePath = null;
                try
                {
                    relativePath = GetRelativePath(inputRoot, file);
                    string outputExtension = GetOutputExtension(file);
                    if (string.IsNullOrWhiteSpace(outputExtension))
                    {
                        summary.Skipped++;
                        continue;
                    }

                    string outputRelativePath = Path.ChangeExtension(relativePath, outputExtension);
                    string outputPath = Path.Combine(outputRoot, outputRelativePath);
                    progress.Report(new BatchProgress { Processed = processed, Total = summary.TotalDiscovered, CurrentFile = relativePath, IsCompleted = false });

                    if (!outputPaths.Add(outputPath))
                    {
                        summary.Skipped++;
                        summary.Errors.Add("Output path collision for " + relativePath);
                        continue;
                    }

                    string outputDirectory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    string errorMessage;
                    if (TryConvertFile(file, outputPath, options, out errorMessage))
                    {
                        summary.Converted++;
                    }
                    else
                    {
                        summary.Failed++;
                        summary.Errors.Add(relativePath + ": " + errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    summary.Failed++;
                    string errorPath = relativePath ?? file;
                    summary.Errors.Add(errorPath + ": " + ex.Message);
                }
                finally
                {
                    processed++;
                    string displayPath = relativePath ?? file;
                    progress.Report(new BatchProgress { Processed = processed, Total = summary.TotalDiscovered, CurrentFile = displayPath, IsCompleted = true });
                }
            }
        }

        private void ShowBatchSummary(BatchSummary summary)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("Batch conversion complete.");
            messageBuilder.AppendLine("Total discovered: " + summary.TotalDiscovered);
            messageBuilder.AppendLine("Converted: " + summary.Converted);
            messageBuilder.AppendLine("Skipped: " + summary.Skipped);
            messageBuilder.AppendLine("Failed: " + summary.Failed);

            if (summary.Errors.Count > 0)
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("Errors:");
                foreach (string error in summary.Errors.Take(5))
                {
                    messageBuilder.AppendLine("- " + error);
                }
                if (summary.Errors.Count > 5)
                {
                    messageBuilder.AppendLine("...and " + (summary.Errors.Count - 5) + " more.");
                }
            }

            MessageBox.Show(messageBuilder.ToString(), "Conversion summary", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private ConversionOptions GetConversionOptions()
        {
            return new ConversionOptions
            {
                IncludeColors = ColorCheckbox.IsChecked == true,
                IncludeUnionData = UnionCheckbox.IsChecked == true,
                ConvertScripts = ScriptConvertCheckbox.IsChecked == true,
                ConvertFolders = ConvertFoldersCheckbox.IsChecked == true,
                ChangeRbxassetid = ChangeRbxassetidCheckbox.IsChecked == true,
                ConvertTextSize = ChangeTextSizeToFontSizeCheckbox.IsChecked == true
            };
        }

        private void SetConvertingState(bool converting)
        {
            isConverting = converting;
            UpdateModeUi();
        }

        private void UpdateModeUi()
        {
            if (BrowseButton == null || PlaceSelectedLabel == null || BrowseFolderButton == null || BrowseOutputFolderButton == null || InputFolderLabel == null || OutputFolderLabel == null || ConvertButton == null || SingleFileRadioButton == null || BatchFolderRadioButton == null)
            {
                return;
            }

            bool isSingleFile = conversionMode == ConversionMode.SingleFile;
            BrowseButton.IsEnabled = isSingleFile && !isConverting;
            PlaceSelectedLabel.IsEnabled = isSingleFile;
            BrowseFolderButton.IsEnabled = !isSingleFile && !isConverting;
            BrowseOutputFolderButton.IsEnabled = !isSingleFile && !isConverting;
            InputFolderLabel.IsEnabled = !isSingleFile;
            OutputFolderLabel.IsEnabled = !isSingleFile;
            ConvertButton.IsEnabled = !isConverting;
            SingleFileRadioButton.IsEnabled = !isConverting;
            BatchFolderRadioButton.IsEnabled = !isConverting;
        }

        private bool TryReadInputFile(string inputPath, out string fileContents, out string errorMessage)
        {
            try
            {
                fileContents = File.ReadAllText(inputPath);
            }
            catch (Exception ex)
            {
                fileContents = null;
                errorMessage = "Failed to read file: " + ex.Message;
                return false;
            }

            if (fileContents.IndexOf("<roblox!", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                errorMessage = "Please select a model or place in Roblox XML format.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private bool TryWriteConvertedFile(string fileContents, string outputPath, ConversionOptions options, out string errorMessage)
        {
            try
            {
                string convertedContents = ConvertContents(fileContents, options);
                File.WriteAllText(outputPath, convertedContents);
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to write converted file: " + ex.Message;
                return false;
            }
        }

        private bool TryConvertFile(string inputPath, string outputPath, ConversionOptions options, out string errorMessage)
        {
            string fileContents;
            if (!TryReadInputFile(inputPath, out fileContents, out errorMessage))
            {
                return false;
            }

            return TryWriteConvertedFile(fileContents, outputPath, options, out errorMessage);
        }

        private string ConvertContents(string fileContents, ConversionOptions options)
        {
            int terrainIndex = fileContents.IndexOf("<Item class=\"Terrain\"", StringComparison.Ordinal);
            if (terrainIndex != -1)
            {
                int terrainEndIndex = fileContents.IndexOf("</Item>", terrainIndex, StringComparison.Ordinal);
                if (terrainEndIndex != -1)
                {
                    fileContents = fileContents.Remove(terrainIndex, terrainEndIndex - terrainIndex + 7);
                }
            }

            if (options.ConvertTextSize)
            {
                int[] FontSizes = new int[10] { 8, 9, 10, 11, 12, 14, 18, 24, 36, 48 };
                Dictionary<int, int> converted = new Dictionary<int, int>();
                Regex rg = new Regex(@"<float name=""TextSize"">(\d{1,3})</float>");
                MatchCollection matchedfloats = rg.Matches(fileContents);

                foreach (Match ItemMatch in matchedfloats)
                {
                    var nearest = FontSizes.OrderBy(x => Math.Abs((long)x - Int32.Parse(ItemMatch.Groups[1].Value))).First();
                    converted[Int32.Parse(ItemMatch.Groups[1].Value)] = nearest;
                }

                foreach (KeyValuePair<int, int> entry in converted)
                {
                    fileContents = fileContents.Replace("<float name=\"TextSize\">" + entry.Key.ToString() + "</float>", "<token name=\"FontSize\">" + Array.IndexOf(FontSizes, entry.Value).ToString() + "</token>");
                }
            }

            if (options.IncludeColors)
            {
                foreach (KeyValuePair<string, string> entry in color3uint8ToBrickColor)
                {
                    fileContents = fileContents.Replace("<Color3uint8 name=\"Color3uint8\">" + entry.Key + "</Color3uint8>", "<int name=\"BrickColor\">" + entry.Value + "</int>");
                }
            }

            if (!options.IncludeUnionData)
            {
                int unionIndex = fileContents.IndexOf("<Item class=\"NonReplicatedCSGDictionaryService\"", StringComparison.Ordinal);
                if (unionIndex != -1)
                {
                    int binaryStringIndex = fileContents.IndexOf("<Item class=\"BinaryStringValue\"", unionIndex, StringComparison.Ordinal);
                    while (binaryStringIndex != -1)
                    {
                        int binaryStringEndIndex = fileContents.IndexOf("</Item>", binaryStringIndex, StringComparison.Ordinal);
                        if (binaryStringEndIndex != -1)
                        {
                            fileContents = fileContents.Remove(binaryStringIndex, binaryStringEndIndex - binaryStringIndex + 7);
                        }
                        binaryStringIndex = fileContents.IndexOf("<Item class=\"BinaryStringValue\"", binaryStringIndex, StringComparison.Ordinal);
                    }
                }
            }

            if (options.ConvertScripts)
            {
                fileContents = fileContents.Replace("<ProtectedString name=\"Source\"><![CDATA[", "<ProtectedString name=\"Source\">");
                fileContents = fileContents.Replace("]]></ProtectedString>", "</ProtectedString>");
                int scriptStartIndex = fileContents.IndexOf("<ProtectedString name=\"Source\">", StringComparison.Ordinal);
                while (scriptStartIndex != -1)
                {
                    int scriptEndIndex = fileContents.IndexOf("</ProtectedString>", scriptStartIndex, StringComparison.Ordinal);
                    if (scriptEndIndex != -1)
                    {
                        string scriptBeforeContents = fileContents.Substring(scriptStartIndex + 31, scriptEndIndex - scriptStartIndex - 31);
                        if (scriptBeforeContents.Length > 0)
                        {
                            string scriptAfterContents = scriptBeforeContents;
                            scriptAfterContents = scriptAfterContents.Replace("\"", "&quot;");
                            scriptAfterContents = scriptAfterContents.Replace("\'", "&apos;");
                            scriptAfterContents = scriptAfterContents.Replace("<", "&lt;");
                            scriptAfterContents = scriptAfterContents.Replace(">", "&gt;");
                            fileContents = fileContents.Replace(scriptBeforeContents, scriptAfterContents);
                        }
                    }
                    scriptStartIndex = fileContents.IndexOf("<ProtectedString name=\"Source\">", scriptEndIndex, StringComparison.Ordinal);
                }
            }

            if (options.ChangeRbxassetid)
            {
                fileContents = fileContents.Replace("rbxassetid://", "http://www.roblox.com/asset/?id=");
            }

            if (options.ConvertFolders)
            {
                fileContents = fileContents.Replace("<Item class=\"Folder\"", "<Item class=\"Model\"");
            }

            return fileContents;
        }

        private static bool IsSubdirectory(string parentPath, string childPath)
        {
            string parentFullPath = EnsureTrailingSeparator(Path.GetFullPath(parentPath));
            string childFullPath = EnsureTrailingSeparator(Path.GetFullPath(childPath));
            return childFullPath.StartsWith(parentFullPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }

        private static string GetRelativePath(string rootPath, string fullPath)
        {
            var rootUri = new Uri(EnsureTrailingSeparator(Path.GetFullPath(rootPath)));
            var fullUri = new Uri(Path.GetFullPath(fullPath));
            string relativePath = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fullUri).ToString());
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string GetOutputExtension(string inputPath)
        {
            string extension = Path.GetExtension(inputPath);
            string outputExtension;
            return outputExtensionByInput.TryGetValue(extension, out outputExtension) ? outputExtension : null;
        }

        private static bool IsSupportedExtension(string inputPath)
        {
            return supportedExtensions.Contains(Path.GetExtension(inputPath));
        }

        private static List<string> DiscoverSupportedFiles(string inputRoot)
        {
            return Directory.EnumerateFiles(inputRoot, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedExtension)
                .ToList();
        }
    }
}
