using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using System.Runtime.InteropServices;

namespace SqlBackup
{
    class Program
    {
        [DllImport("user32.dll")]
        public static extern IntPtr
        FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        static void Main(string[] args)
        {
            Console.Title = "sqlbackup";

            /*
            server="(local)" 
            login= 
            password= 
            db="2d1 OdessaCarHire" 
            zip=1
            ratio=9 
            backuptype=F
            output="c:\backup"
            savelog=1
            showlog=1
            count=0
            hidewin=0
            */

            var log = new List<string>();
            var parser = new CommandLineParser();
            string server = parser.GetValue<string>("server", args);
            string login = parser.GetValue<string>("login", args);
            string password = parser.GetValue<string>("password", args);
            string[] dbs = parser.GetValue<string>("db", args).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string backuptype = parser.GetValue<string>("backuptype", args);
            backuptype = string.IsNullOrEmpty(backuptype) ? "F" : backuptype;

            string zip = parser.GetValue<string>("zip", args);
            zip = string.IsNullOrEmpty(zip) ? "1" : zip;

            string ratio = parser.GetValue<string>("ratio", args);
            ratio = string.IsNullOrEmpty(ratio) ? "5" : ratio;

            string output = parser.GetValue<string>("output", args);

            string savelog = parser.GetValue<string>("savelog", args);
            savelog = string.IsNullOrEmpty(savelog) ? "1" : savelog;
            var saveLog = savelog == "1";

            string showlog = parser.GetValue<string>("showlog", args);
            showlog = string.IsNullOrEmpty(showlog) ? "1" : showlog;
            var showLog = showlog == "1";

            string hidewin = parser.GetValue<string>("hidewin", args);
            hidewin = string.IsNullOrEmpty(hidewin) ? "0" : hidewin;
            var hideWin = hidewin == "1";

            string count = parser.GetValue<string>("count", args);
            count = string.IsNullOrEmpty(count) ? "0" : count;

            IntPtr hWnd = FindWindow(null, Console.Title);
            if (hWnd != IntPtr.Zero) ShowWindow(hWnd, hideWin ? 0 : 1); // 0 = SW_HIDE

            Helper.SetLog("Welcome arunes sql backup command line utility", ref log, showLog);

            if (string.IsNullOrEmpty(server))
                Helper.SetLog("please specify server. (ex: server=(local))", ref log, showLog);
            else if (dbs.Length == 0)
                Helper.SetLog("please specify sql server database(s). (ex: db=\"testdb1 testdb2\")", ref log, showLog);
            else if (string.IsNullOrEmpty(output))
                Helper.SetLog("please specify output folder. (ex: output=\"c:\\backup\")", ref log, showLog);
            else
            {
                var trusted = false;
                short sRatio = 5;
                short sCount = 0;
                trusted = string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password);
                if (trusted) Helper.SetLog("no login provided, switching trusted connection..", ref log, showLog);

                if (backuptype != "F" && backuptype != "D" && backuptype != "L")
                {
                    Helper.SetLog("backup type cannot determined! please specify one of following parameters: F for Full Backup, D for Differential Backup, L for Log Backup. (ex: backuptype:F)", ref log, showLog);
                    Environment.Exit(0);
                }

                if (!Int16.TryParse(count, out sCount))
                {
                    Helper.SetLog("count cannot determined! please specify valid count (from 0 (unlimited) to 50)", ref log, showLog);
                    Environment.Exit(0);
                }

                if ((sCount < 0 || sCount > 50))
                {
                    Helper.SetLog("count cannot determined! please specify valid count (from 0 (unlimited) to 50)", ref log, showLog);
                    Environment.Exit(0);
                }

                if (!Int16.TryParse(ratio, out sRatio))
                {
                    Helper.SetLog("ratio cannot determined! please specify valid ratio (from 0 (store) to 9 (best))", ref log, showLog);
                    Environment.Exit(0);
                }

                if ((sRatio < 0 || sRatio > 9))
                {
                    Helper.SetLog("ratio cannot determined! please specify valid ratio (from 0 (store) to 9 (best))", ref log, showLog);
                    Environment.Exit(0);
                }

                if (!Directory.Exists(output))
                {
                    Helper.SetLog("output directory not exists, trying to create..", ref log, showLog);
                    try
                    {
                        Directory.CreateDirectory(output);
                        Helper.SetLog("output directory created {0}", ref log, showLog, output);
                    }
                    catch
                    {
                        Helper.SetLog("output directory cannot be created! please create manually.", ref log, showLog);
                        Environment.Exit(0);
                    }
                }

                var error = false;
                foreach (var db in dbs)
                {
                    var backupSuccess = false;
                    var fileName = Helper.GetNewFilePath(backuptype, output, db);

                    Helper.SetLog("trying to connect sql server for {0}", ref log, showLog, db);
                    using (SqlConnection conn = new SqlConnection(Helper.GetConnectionString(server, db, login, password)))
                    {
                        try
                        {
                            conn.Open();
                            Helper.SetLog("sql connection successfully for {0}, trying to make backup..", ref log, showLog, db);
                            var sqlStr = Helper.GetBackupSql(backuptype, fileName, output, db);

                            SqlCommand cmd = new SqlCommand(sqlStr, conn);
                            try
                            {
                                cmd.ExecuteNonQuery();
                                Helper.SetLog("backup for db {0} saved successfully to {1}", ref log, showLog, db, output);
                                backupSuccess = true;

                                if (zip == "1")
                                {
                                    try
                                    {
                                        Helper.SetLog("compressing file {0}", ref log, showLog, fileName);
                                        Helper.ZipFile(fileName, sRatio);

                                        try
                                        { // delete bak file
                                            cmd.CommandText = string.Format("EXECUTE master.dbo.xp_delete_file 0, '{0}'", fileName);
                                            cmd.ExecuteNonQuery();
                                        }
                                        catch (Exception ex)
                                        {
                                            Helper.SetLog("error when deleting bak file {0}, please delete manually. error details: {1}", ref log, showLog, fileName, ex.Message);
                                            error = true;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Helper.SetLog("error when compressing bak file {0}. error details: {1}", ref log, showLog, fileName, ex.Message);
                                        error = true;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Helper.SetLog("error occurred when making backup for db {0}, error details: {1}", ref log, showLog, db, ex.Message);
                                error = true;
                            }

                            conn.Close();
                        }
                        catch (Exception ex)
                        {
                            Helper.SetLog("cannot connect db {0}, error details: {1}", ref log, showLog, db, ex.Message);
                            error = true;
                        }
                    }

                    if (backupSuccess && sCount > 0)
                    { // check occurances 
                        var allBackups = Helper.GetBackupFiles(output, db);
                        if (sCount < allBackups.Count())
                        {
                            foreach (var bFile in allBackups.Skip(sCount))
                                File.Delete(bFile.Value);
                        }
                    }
                }

                Helper.SetLog("backup process ended {0}", ref log, showLog, error ? "with error(s)." : "successfully.");
                if (saveLog) File.WriteAllLines(Path.Combine(output, DateTime.Now.ToString("yyyyMMddHHmm") + ".log"), log.ToArray());
            }

            Environment.Exit(0);
        }
    }
}
