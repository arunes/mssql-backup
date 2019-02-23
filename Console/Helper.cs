using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using ICSharpCode.SharpZipLib.Zip;
using System.Text.RegularExpressions;

namespace SqlBackup
{
    static class Helper
    {
        internal static void SetLog(string message, ref List<string> logs, bool write, params object[] args)
        {
            var sMessage = string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), string.Format(message, args));
            logs.Add(sMessage);
            if (write) Console.WriteLine(sMessage);
        }

        internal static string GetNewFilePath(string backupType, string output, string db)
        {
            var fileName = string.Format("{0}_{1}_{2}.bak", db, backupType, DateTime.Now.ToString("yyyyMMddHHmm"));
            fileName = Path.Combine(output, fileName);

            return fileName;
        }

        internal static string GetConnectionString(string server, string db, string login, string password)
        {
            var loginInfo = "uid={0};password={1};";
            var trusted = "Trusted_Connection=True;";

            return string.Format("server={0};initial catalog={1};{2}",
                server, db,
                (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password)) ? trusted :
                    string.Format(loginInfo, login, password));
        }

        internal static string GetBackupSql(string backupType, string fileName, string output, string db)
        {
            switch (backupType)
            {
                case "F":
                    return string.Format("BACKUP DATABASE [{0}] TO DISK = '{1}'", db, fileName);
                case "D":
                    return string.Format("BACKUP DATABASE [{0}] TO DISK = '{1}' WITH DIFFERENTIAL", db, fileName);
                case "L":
                    return string.Format("BACKUP LOG [{0}] TO DISK = '{1}'", db, fileName);
            }

            return null;
        }

        internal static void ZipFile(string inputFilePath, int ratio)
        {
            var outputFilePath = Path.ChangeExtension(inputFilePath, "zip");
            FileStream ostream;
            byte[] obuffer;
            using (ZipOutputStream oZipStream = new ZipOutputStream(File.Create(outputFilePath)))
            {
                oZipStream.SetLevel(ratio); // maximum compression
                ZipEntry oZipEntry;

                oZipEntry = new ZipEntry(Path.GetFileName(inputFilePath));
                oZipStream.PutNextEntry(oZipEntry);

                using (FileStream fStream = File.OpenRead(inputFilePath))
                {
                    ostream = fStream;
                    obuffer = new byte[ostream.Length];
                    ostream.Read(obuffer, 0, obuffer.Length);
                    oZipStream.Write(obuffer, 0, obuffer.Length);

                    fStream.Close();
                }

                oZipStream.Finish();
                oZipStream.Close();
            }
        }

        internal static IOrderedEnumerable<KeyValuePair<DateTime, string>> GetBackupFiles(string output, string db)
        {
            var retVal = new System.Collections.Generic.Dictionary<DateTime, string>();
            var allBackups = new List<string>();
            allBackups.AddRange(Directory.GetFiles(output, "*.zip"));
            allBackups.AddRange(Directory.GetFiles(output, "*.bak"));

            foreach (var bFile in allBackups)
            {
                if (Regex.IsMatch(Path.GetFileNameWithoutExtension(bFile), string.Format(@"{0}_._\d+", db)))
                {
                    var fDate = Path.GetFileNameWithoutExtension(bFile).Split('_')[Path.GetFileNameWithoutExtension(bFile).Split('_').Length - 1];
                    var properStr = fDate.Insert(4, "-").Insert(7, "-").Insert(10, " ").Insert(13, ":");
                    var date = DateTime.MinValue;

                    if (DateTime.TryParse(properStr, out date) && !retVal.ContainsKey(date))
                        retVal.Add(date, bFile);
                }
            }

            return retVal.OrderByDescending(x => x.Key);
        }
    }
}
