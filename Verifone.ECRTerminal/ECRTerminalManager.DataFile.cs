using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Verifone.ECRTerminal
{
    public partial class ECRTerminalManager
    {
        /// <summary>
        /// Validates a directory path for basic safety: non-empty, no invalid path/file name chars,
        /// optional relative allowance, and no trailing space/dot in the last segment.
        /// Uses <see cref="Path.GetFullPath(string)"/> to normalize before checks.
        /// </summary>
        /// <param name="path">The directory path to validate.</param>
        /// <param name="allowRelative">If true, relative paths are allowed; otherwise the path must be rooted.</param>
        /// <returns><c>true</c> if the path appears valid; otherwise <c>false</c>.</returns>

        protected bool IsValidDirectoryPath(string path, bool allowRelative = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return false;

            try
            {
                string fullPath = Path.GetFullPath(path);

                // Use fullPath here
                if (!allowRelative && !Path.IsPathRooted(fullPath))
                    return false;

                // last segment check (protect against trailing space/dot issues as well)
                string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string lastSegment = Path.GetFileName(trimmed);

                if (!string.IsNullOrEmpty(lastSegment))
                {
                    if (lastSegment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        return false;

                    // Windows forbids trailing spaces/dots in path components
                    if (lastSegment.EndsWith(" ", StringComparison.Ordinal) ||
                        lastSegment.EndsWith(".", StringComparison.Ordinal))
                        return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// File name extension used for persisted transaction records (including the leading dot),
        /// e.g. <c>.ecrtn.txt</c>. Subclasses may override to read/write alternative extensions.
        /// </summary>
        protected virtual string TransactionFileExtension => ".ecrtn.txt";

        /// <summary>
        /// Builds a unique transaction file path under the given directory using the pattern
        /// <c>yyyy-MM-dd-HH-mm-ss-transactionId[-counter]</c> plus <see cref="TransactionFileExtension"/>.
        /// </summary>
        /// <param name="directoryFullPath">Target directory (relative or absolute). Will be normalized to a full path.</param>
        /// <param name="data">Transaction information used to derive timestamp and transactionId.</param>
        /// <returns>Full file path to a non-existent file name candidate, or <c>null</c> on error.</returns>
        protected string GetTransactionFilePath(string directoryFullPath, TransactionResultExEventArgs data)
        {
            string result = null;

            try
            {
                string timestamp = data.TransactionDateTime.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
                string baseName = timestamp + "-" + data.TransactionId + "-" + data.TransactionType;
                string fileName = baseName + TransactionFileExtension;
                result = Path.Combine(directoryFullPath, fileName);

                int counter = 0;

                while (File.Exists(result))
                {
                    counter++;
                    fileName = baseName + "-" + counter + TransactionFileExtension;
                    result = Path.Combine(directoryFullPath, fileName);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(GetTransactionFilePath)}:\n{ex}", GetType().FullName);
            }

            return result;
        }

        /// <summary>
        /// Serializes and saves the supplied transaction record to <see cref="TransactionResultEventArgs.TransactionFilePath"/>
        /// using UTF-8 without BOM and <see cref="FileMode.CreateNew"/>.
        /// </summary>
        /// <param name="data">Transaction record with <c>TransactionFilePath</c> already set.</param>
        protected void SaveTransaction(TransactionResultEventArgs data)
        {
            try
            {
                string text = TransactionResultTextSerializer.Serialize(data);
                using (FileStream fs = new FileStream(data.TransactionFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(text);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(SaveTransaction)}:\n{ex}", GetType().FullName);
            }
        }


        /// <summary>
        /// Filename pattern matcher for persisted transaction records:
        /// <c>yyyy-MM-dd-HH-mm-ss-transactionId-transactionType[-counter]</c> plus <see cref="TransactionFileExtension"/>.
        /// The optional numeric suffix avoids collisions when multiple files share the same timestamp/id.
        /// Compiled and culture-invariant for performance and stability.
        /// </summary>
        protected Regex TransactionFileRegex =>
            new Regex(@"^\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}-[^-]+-[^.]+(?:-\d+)?" + Regex.Escape(TransactionFileExtension) + "$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Enumerates and deserializes all transaction result files in the specified directory
        /// whose names match the expected timestamped pattern (<c>yyyy-MM-dd-HH-mm-ss-transactionId-transactionType[-counter]</c> plus <see cref="TransactionFileExtension"/>).
        /// Files are read as UTF-8; unreadable or invalid files are skipped with a trace entry. The first line must be exactly <c>[TransactionInfo]</c>.
        /// </summary>
        /// <param name="directoryPath">Absolute or relative directory path containing persisted transaction files.</param>
        /// <returns>
        /// A sequence of deserialized <see cref="TransactionResultEventArgs"/> objects,  
        /// or an empty sequence if the directory does not exist or no valid files are found.
        /// </returns>
        public IEnumerable<TransactionResultEventArgs> GetTransactions(string directoryPath)
        {
            Trace.WriteLine($"{nameof(GetTransactions)}", GetType().FullName);

            if (string.IsNullOrWhiteSpace(directoryPath))
                return Enumerable.Empty<TransactionResultEventArgs>();

            string fullDir;
            try { fullDir = Path.GetFullPath(directoryPath); }
            catch { return Enumerable.Empty<TransactionResultEventArgs>(); }

            if (!Directory.Exists(fullDir))
                return Enumerable.Empty<TransactionResultEventArgs>();

            IEnumerable<string> candidates = Directory.EnumerateFiles(fullDir, $"*{TransactionFileExtension}", SearchOption.TopDirectoryOnly);
            IEnumerable<string> filtered = candidates.Where(path => TransactionFileRegex.IsMatch(Path.GetFileName(path)));

            List<TransactionResultEventArgs> results = new List<TransactionResultEventArgs>();
            foreach (string path in filtered)
            {
                try
                {
                    // Header check (cheap authenticity guard). Use BOM-tolerant reader.
                    using (var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true))
                    {
                        string firstLine = reader.ReadLine();
                        if (!string.Equals(firstLine?.Trim(), "[TransactionInfo]", StringComparison.Ordinal))
                            continue;

                        // Read the rest of the content
                        string fileContent = firstLine + Environment.NewLine + reader.ReadToEnd();

                        TransactionResultEventArgs record = TransactionResultTextSerializer.Deserialize(fileContent);
                        if (record != null)
                        {
                            record.TransactionFilePath = path; // runtime metadata only; not serialized
                            results.Add(record);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{nameof(GetTransactions)}:\n{ex}", GetType().FullName);
                }
            }

            return results;
        }

        /// <summary>
        /// Enumerates and deserializes all transaction result files from the manager’s configured data directory.
        /// This is a convenience overload of <see cref="GetTransactions(string)"/> that uses the internally configured path.
        /// </summary>
        /// <returns>
        /// A sequence of deserialized <see cref="TransactionResultEventArgs"/> objects,  
        /// or an empty sequence if the directory does not exist or no valid files are found.
        /// </returns>
        public IEnumerable<TransactionResultEventArgs> GetTransactions()
        {
            return GetTransactions(_dataDirectoryPath);
        }
    }
}