﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Data.OracleClient;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Security.Cryptography;

namespace ZLCHSLisComm1
{
    /// <summary>
    /// Oracle数据操作类
    /// </summary>
    public class OracleHelper
    {
        private static OracleConnection conn = null;
        private static OracleCommand cmd = null;
        static string  FileName;
        private static byte[] Keys = { 0xEF, 0xAB, 0x56, 0x78, 0x90, 0x34, 0xCD, 0x12 };
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);
        /// <summary>
        /// 获取数据库连接字符串
        /// </summary>
        /// <returns></returns>
        public static string GetConnectionstring()
        {
            string Return_Value;
            string strUser;
            string strPassword;
            string strServer;
            string inifile = "SOLVESET.INI";


            Return_Value = INIExists(inifile);
            if (!String.IsNullOrEmpty(Return_Value)) return Return_Value;

            StringBuilder objStrBd = new StringBuilder(256);

            long OpStation = GetPrivateProfileString("EQUIPMENT", "DBSERVER", "", objStrBd, 256, FileName);
            if (OpStation == 0)                return "未找到段落【EQUIPMENT】或关键字【DBSERVER】!";
            strServer = objStrBd.ToString();
            strServer = DecryptDES(strServer, "zlsofthis");

            OpStation = GetPrivateProfileString("EQUIPMENT", "LOGID", "", objStrBd, 256, FileName);
            if (OpStation == 0)                return "未找到段落【EQUIPMENT】或关键字【LOGID】!";
            strUser = objStrBd.ToString();
            strUser = DecryptDES(strUser, "zlsofthis");

            OpStation = GetPrivateProfileString("EQUIPMENT", "LOGPASS", "", objStrBd, 256, FileName);
            if (OpStation == 0)                return "未找到段落【EQUIPMENT】或关键字【LOGPASS】!";
            strPassword = objStrBd.ToString();
            strPassword = DecryptDES(strPassword, "zlsofthis");

            string connStr = "Data Source=" + strServer + ";User Id=" + strUser + ";Password=" + strPassword + ";Integrated Security=no;";
            return connStr;

        }
        /// <summary>
        /// 执行SQL语句并返回记录集
        /// </summary>
        /// <param name="cmdStr">标准的SQL语句</param>
        /// <returns></returns>
        public static DataTable GetDataTable(string cmdStr)
        {
            using (OracleConnection _connection = new OracleConnection(GetConnectionstring()))
            {
                DataTable dt = new DataTable();
                try
                {
                    _connection.Open();
                    OracleDataAdapter da = new OracleDataAdapter();
                    da.SelectCommand = new OracleCommand(cmdStr, _connection);                    
                    da.Fill(dt);
                }
                catch (Exception exs)
                {
                    //ClassLib.Error.RedirectError(exs.Message.ToString(), "", "");
                    // throw new Exception(exs.Message.ToString());
                }
                finally { _connection.Close(); }
                return dt;
            }

        }
        /// <summary>
        /// 执行SQL语句
        /// </summary>
        /// <param name="cmdStr"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static bool OraExeNonQuery(ArrayList cmdStr, ref string msg)
        {
            bool _ret = false;
            OracleConnection _connection = new OracleConnection(GetConnectionstring());
            try
            {
                _connection.Open();
                OracleTransaction _trans = _connection.BeginTransaction();
                OracleCommand _command = _connection.CreateCommand();
                _command.Transaction = _trans;
                try
                {
                    for (int i = 0; i < cmdStr.Count; i++)
                    {
                        _command.CommandText = cmdStr[i].ToString();
                        _command.ExecuteNonQuery();
                    }
                    _trans.Commit();
                    _ret = true;
                    msg = "执行成功!";
                }
                catch (Exception ex1)
                {
                    _trans.Rollback();
                    msg = ex1.Message.ToString();
                }
            }
            catch (Exception ex)
            {
                msg = ex.Message.ToString();
            }
            finally
            {
                _connection.Close(); _connection.Dispose();
            }
            return _ret;
        }
        /// <summary>
        /// 执行SQL语句
        /// </summary>
        /// <param name="cmdStr"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static bool OraExeNonQuery(string cmdStr, ref string msg)
        {
            bool _ret = false;
            OracleConnection _connection = new OracleConnection(GetConnectionstring());
            try
            {
                _connection.Open();
                OracleTransaction _trans = _connection.BeginTransaction();
                OracleCommand _command = _connection.CreateCommand();
                _command.Transaction = _trans;
                try
                {
                    _command.CommandText = cmdStr;
                    _command.CommandType = CommandType.Text;
                    _command.ExecuteNonQuery();
                    _trans.Commit();
                    _ret = true;
                    msg = "执行成功!";
                }
                catch (Exception ex1)
                {
                    _trans.Rollback();
                    msg = ex1.Message.ToString();
                }
            }
            catch (Exception ex)
            {
                msg = ex.Message.ToString();
            }
            finally
            {
                _connection.Close(); _connection.Dispose();
            }
            return _ret;
        }
        /// <summary>
        /// 执行SQL语句
        /// </summary>
        /// <param name="cmdStr"></param>
        /// <param name="parameters"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static bool OraExeNonQuery(string cmdStr, OracleParameter[] parameters, ref string msg)
        {
            bool _ret = false;
            conn = new OracleConnection(GetConnectionstring());
            try
            {
                conn.Open();
                OracleTransaction _trans = conn.BeginTransaction();
                cmd = new OracleCommand();
                cmd.Connection = conn;
                cmd.CommandText = cmdStr;//声明存储过程名
                cmd.CommandType = CommandType.Text;
                cmd.Transaction = _trans;
                foreach (OracleParameter parameter in parameters)
                {
                    cmd.Parameters.Add(parameter);
                }
                try
                {
                    cmd.ExecuteNonQuery();//执行存储过程
                    _trans.Commit();
                    _ret = true;
                    msg = "执行成功!";
                }
                catch (Exception ex)
                {
                    msg = ex.Message.ToString();
                    _trans.Rollback();
                }
            }
            catch (Exception e)
            {
                msg = e.Message.ToString();
            }
            finally { conn.Close(); conn.Dispose(); }
            return _ret;
        }
        /// <summary>
        /// 执行存储过程
        /// </summary>
        /// <param name="storedProcName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static bool RunProcedure(string storedProcName, OracleParameter[] parameters,ref string msg)
        {
            bool _ret = false;
            conn = new OracleConnection(GetConnectionstring());
            try
            {
                conn.Open();
                OracleTransaction _trans = conn.BeginTransaction();
                cmd = new OracleCommand();
                cmd.Connection = conn;
                cmd.CommandText = storedProcName;//声明存储过程名
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Transaction = _trans;
                foreach (OracleParameter parameter in parameters)
                {
                    cmd.Parameters.Add(parameter);
                }
                try
                {
                    cmd.ExecuteNonQuery();//执行存储过程
                    _trans.Commit();
                    _ret = true;
                    msg = "";
                }
                catch (Exception ex)
                {
                    msg = ex.Message.ToString();
                    _trans.Rollback();                    
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally { conn.Close(); conn.Dispose(); }
            return _ret;
        }
        /// <summary>
        /// 判断配置文件是否存在
        /// </summary>
        /// <param name="AFileName"></param>
        public static string INIExists(string AFileName)
        {
            AFileName = System.AppDomain.CurrentDomain.BaseDirectory.ToString() + AFileName;
            FileInfo fileInfo = new FileInfo(AFileName);
            if ((!fileInfo.Exists))
            {   //文件不存在
                return "未找到配置文件[" + AFileName + "]";
            }
            //必须是完全路径，不能是相对路径
            FileName = fileInfo.FullName;
            return "";
        }
        ///　<summary>
        ///　DES解密字符串
        ///　</summary>
        ///　<param　name="decryptString">待解密的字符串</param>
        ///　<param　name="decryptKey">解密密钥,要求为8位,和加密密钥相同</param>
        ///　<returns>解密成功返回解密后的字符串，失败返源串</returns>
        public static string DecryptDES(string decryptString, string decryptKey)
        {
            try
            {
                byte[] rgbKey = Encoding.UTF8.GetBytes(decryptKey.Substring(0, 8));
                byte[] rgbIV = Keys;
                byte[] inputByteArray = Convert.FromBase64String(decryptString);
                DESCryptoServiceProvider DCSP = new DESCryptoServiceProvider();
                MemoryStream mStream = new MemoryStream();
                CryptoStream cStream = new CryptoStream(mStream, DCSP.CreateDecryptor(rgbKey, rgbIV), CryptoStreamMode.Write);
                cStream.Write(inputByteArray, 0, inputByteArray.Length);
                cStream.FlushFinalBlock();
                return Encoding.UTF8.GetString(mStream.ToArray());

            }
            catch
            {
                return decryptString;
            }
        }
    }
}
