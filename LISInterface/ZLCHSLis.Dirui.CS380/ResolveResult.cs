﻿
/*************************************
 * 名称：迪瑞.CS380生化仪解析程序
 * 功能：CS380生化分析仪.肝功.心肌酶谱等
 * 作者：谢天
 * 时间：2017-12-04
 * 通讯类型：串口  9600  8 1 N
 * 备注:设备需要安装虚拟串口,通过虚拟串口进行数据交互 
 * ***********************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Text.RegularExpressions;
using ZLCHSLisComm;
using System.Collections;
using System.IO;
using System.Xml;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;


namespace ZLCHSLis.Dirui.CS380
{
    public class ResolveResult : IDataResolve
    {
        public string strInstrument_id;
        public string strSubBegin;  //多帧开始位
        public string strSubEnd;    //多帧结束位
        public string strDetype;    //解析方式 
        public string strDataBegin;  //数据开始位
        public string strDataEnd;                 //数据结束位
        //public string strBeginBits;              //多帧时开始位
        //public string strEndBits;                 //多帧时结束位
        public string strACK_all;                  //全部应答
        public string strACK_term;                 //条件应答
        string yangbenhaoStart = "";
        string tiaomahao = "";
        string yangbenhaoEnd = "";
        public List<string> listInputResult = new List<string>();
        public List<string> ListImagePosition = new List<string>();              //图像存放位置
        public Boolean ImmediatelyUpdate = false;                               //立即更新
        Mindray may = new Mindray();
        DataSetHandle dsHandle = new DataSetHandle();
        Write_Log writelog = new Write_Log();
        SaveResult saveResult;
        string TestResultValue;      //解析后的通用字符串
        string strDevice;
        string strDeviceID;
        List<string> TestGraph; //图像列表

        DataRow DrTestTimeSignField;         //检验时间标识
        DataRow DrTestTimeField;             //检验时间
        DataRow DrSampleNoSignField;         //常规样本号标识
        DataRow DrSampleNoField;             //常规样本号
        DataRow DrBarCodeSignField;             //条码号标识
        DataRow DrBarCodeField;              //条码号
        DataRow DrSampleTypeSignField;       //样本类型标识
        DataRow DrSampleTypeField;           //样本类型
        DataRow DrOperatorSignField;       //检验人标识
        DataRow DrOperatorField;           //检验人
        DataRow DrSpecimenSignField;       //标本标识
        DataRow DrSpecimenField;           //标本
        DataRow DrResultSignField;           //结果标识
        DataRow DrResultInfoField;           //结果信息
        DataRow DrResultCountField;          //结果数
        DataRow DrSingleResultField;         //单个结果
        DataRow DrChannelField;              //通道号
        DataRow DrResultField;               //结果值
        DataRow DrQCSampleField;             //质控样本号



        string strTestTime; //检验时间
        string strSampleNo;//标本号
        string strBarCode;  //条码
        string strOperator; //检验医师
        string strSampleType; //检验类型
        string StrSpecimen;   //标本类型
        string FilePath = "";
        Boolean ResultFlag;                 //结果开始标志
        string subString;                    //临时存放解析字符串
        DataRow[] FindRow;                  //解析设置
        DataSet ds_ItemChannel = new DataSet();
        DataTable tItemChannel = new DataTable();
        Dictionary<String, String> _DIC = new Dictionary<String, String>();


        //ASTM协议常用变量
        // char SB = Convert.ToChar(11);//开始符
        //   char EB = Convert.ToChar(28);//结束符
        char STX = Convert.ToChar(2);//帧开始符
        char CR = Convert.ToChar(13);//回车符       
        char ETX = Convert.ToChar(3);//帧结束符
        char ETQ = Convert.ToChar(5);//查询符
        char ETB = Convert.ToChar(23);//传送块结束
        char EOT = Convert.ToChar(4);//结束符
        char LF = Convert.ToChar(10);//换行符
        char FRAME = '|';//域行符
        string fengefu = "---fengefu---";
        char ENQ = Convert.ToChar(5);//ENQ控制指令
        char ACK = Convert.ToChar(6);//应答符
        char NAK = Convert.ToChar(21);//否定应答符
        public void ParseResult()
        {

            throw new NotImplementedException();
        }
        //计算数据帧开始到结束进行校验返回CS     
        public string CSValidate(string input)
        {
            string output = "";
            byte[] inputArray = Encoding.Default.GetBytes(input);

            int num = 0;
            for (int i = 0; i < inputArray.Length; i++)
            {
                int value = Convert.ToInt32(inputArray[i]);
                num = num + value;
            }
            int cs = num % 256;
            int ys = cs % 16;
            int zs = cs / 16;
            output = zs.ToString("X") + "" + ys.ToString("X");
            return output.ToLower();
        }

        public void ParseResult(string strSource, ref string strResult, ref string strReserved, ref string strCmd)
        {
            string splite_lis = "L|1|N";
            strResult += strSource;           
            //数据接收完成
            if (strSource.EndsWith(EOT + "") || strSource.EndsWith("test-end")||strSource.EndsWith(Convert.ToChar(4)+""))
            {
              
                strSource = strResult;

                writelog.Write(strDevice, "全部数据:" + strSource, "log");
               string[] strResultArray = strResult.Split(new string[] { splite_lis }, StringSplitOptions.RemoveEmptyEntries);
               strResult = "";
               for (int wfw = 0; wfw < strResultArray.Length; wfw++)
               {
                   if (strResultArray [wfw].IndexOf("|CS-380|")==-1) continue;
                   strSource = strResultArray[wfw];
                   writelog.Write(strDevice, "本次数据第" + (wfw +1)+ "条" + strSource, "log");
                   string strTestTimeNow = DateTime.Now.ToString("yyyyMMddHHmmss");
                   string strTestTimeNow1 = DateTime.Now.ToString("yyyy-MM-dd");
                   //支持多个样本模式以"L|1|N<CR><ETX>"作为分隔符


                   string subStrSource = strSource;
                       //首先判断消息类型
                   string[] strSourceArray = strSource.Split(new char[] { LF }, StringSplitOptions.RemoveEmptyEntries);

                       //取出消息头
                       #region//1.病人测试结果
                       try
                       {
                           StreamReader sr = new StreamReader(@".\Dirui.CS380.txt", Encoding.Default);
                           //解析结果检验项目标识符和zlchs定义的变量对应 lis,zlchs|lis1,zlchs1
                           StringBuilder sBuilder = new StringBuilder("");
                           string correspondence = sr.ReadLine();
                           sr.Close();
                           string times = "";
                           string[] correspondenceArray = correspondence.Split('|');
                           //检验时间


                           for (int i = 0; i < strSourceArray.Length; i++)
                           {
                               string header = strSourceArray[i].Split('|')[0];
                               //取消息头
                               if (header.EndsWith("H"))
                               {

                                   times = strSourceArray[i].Split('|')[12];
                                   strTestTime = times.Substring(0, 4) + "-" + times.Substring(4, 2) + "-" + times.Substring(6, 2);
                               }
                               if (header.EndsWith("O"))
                               {
                                   //样本条码^样本编号^盘号^位置^是否稀释  ^1^1^1^N
                                   string OInfo = strSourceArray[i].Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries)[2];
                                   strSampleNo = OInfo.Split('^')[1];


                                   //if ("".Equals(strSourceArray[i].Split('|')[6])) { times = DateTime.Now.ToString("yyyyMMddHHmmss"); }
                                   //else { times = strSourceArray[i].Split('|')[6]; }


                                   //如果有条码号，取核收时间作为检验时间
                                   string sampleBarstr = OInfo.Split('^')[0];
                                   if (!string.IsNullOrEmpty(sampleBarstr))
                                   {
                                       IniFile ConfigIni = new IniFile("SOLVESET.INI");
                                       string encode = ConfigIni.IniReadValue("EQUIPMENT", "Encode");
                                       DataSet ds = new DataSet();
                                       string orgId = ConfigIni.IniReadValue("EQUIPMENT", "Agencies");
                                       string sql = @"to_char(核收时间,'yyyymmddhh24miss') as 核收时间,样本序号";
                                       string where = @" 机构id='" + orgId + "' and  样本条码='" + sampleBarstr + "'";
                                       ds = dsHandle.GetDataSet(sql, "检验记录", where);
                                       times = ds.Tables[0].Rows[0]["核收时间"].ToString();//20160202145321
                                       strSampleNo = ds.Tables[0].Rows[0]["样本序号"].ToString();
                                   }
                                   strTestTime = times.Substring(0, 4) + "-" + times.Substring(4, 2) + "-" + times.Substring(6, 2);
                               }
                               if (header.EndsWith("R"))
                               {
                                   string yiqi = strSourceArray[i].Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries)[2].Split('^')[3];
                                   string yiqiValue = strSourceArray[i].Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries)[3];
                                   for (int j = 0; j < correspondenceArray.Length; j++)
                                   {
                                       string lis = correspondenceArray[j].Split(',')[0].Trim();
                                       string zlchs = correspondenceArray[j].Split(',')[1].Trim();
                                       if (yiqi.Equals(lis))
                                       {
                                           sBuilder.Append(zlchs + ',' + yiqiValue + '|');
                                       }
                                   }
                               }
                           }
                           string str = sBuilder.ToString().Remove(sBuilder.Length - 1);
                           string[] strs = str.Split('|');
                           string ChannelType = "";     //0-普通结果;1-直方图;2-散点图;3-直方图界标;4-散点图界标;5-BASE64
                           string testItemID = "";
                           string TestResultValue = "";
                           for (int i = 0; i < strs.Length; i++)
                           {

                               FindRow = tItemChannel.Select("通道编码='" + strs[i].Split(',')[0] + "'");
                               if (FindRow.Length == 0) //无普通结果则查找图像能道，无图像通道则更新通道类型为空
                               {
                                   ChannelType = null;
                                   writelog.Write(strDevice, "未设置通道：" + strs[i].Split(',')[0], "log");
                               }
                               else
                               {
                                   testItemID = FindRow[0]["项目id"].ToString();
                                   ChannelType = "0"; //普通结果
                                   TestResultValue = TestResultValue + testItemID + "^" + strs[i].Split(',')[1].Replace("RuPT", "阴性") + "|";
                               }

                           }
                           TestResultValue = strTestTime + "|" + strSampleNo + "^" + strSampleType + "^" + strBarCode + "|" + strOperator + "|" + StrSpecimen + "|" + "|" + TestResultValue;
                           saveResult = new SaveResult();
                           if (!string.IsNullOrEmpty(strSampleNo) || !string.IsNullOrEmpty(strBarCode))
                           {
                               saveResult.SaveTextResult(strInstrument_id, TestResultValue, TestGraph, DrSampleNoField);
                               if (ImmediatelyUpdate)
                               {
                                   saveResult.UpdateData();
                               }
                           }
                           if (!ImmediatelyUpdate)
                           {
                               saveResult.UpdateData();
                           }
                       }
                       catch (Exception e)
                       {
                           writelog.Write(strDevice, "处理失败： " + e.ToString(), "log");
                           strResult = "";
                       }

                       #endregion
            
               }
                
            }
            strCmd = ACK + "";


        }

        public System.Drawing.Image LocalIMG(string IMG)
        {
            throw new NotImplementedException();
        }

        public string GetCmd(string dataIn, string ack_term)
        {
            throw new NotImplementedException();
        }

        public void GetRules(string StrDevice)
        {
            DataSet dsTestItem = new DataSet();
            DataSet dsRules = new DataSet();
            strInstrument_id = StrDevice;

            DrTestTimeSignField = null;         //检验时间标识
            DrTestTimeField = null;             //检验时间
            DrSampleNoSignField = null;
            DrSampleNoField = null;
            DrBarCodeSignField = null;
            DrBarCodeField = null;
            DrSampleTypeSignField = null;
            DrSampleTypeField = null;
            DrOperatorSignField = null;
            DrOperatorField = null;
            DrSpecimenSignField = null;
            DrSpecimenField = null;

            DrResultSignField = null;
            DrResultInfoField = null;
            DrResultCountField = null;
            DrSingleResultField = null;
            DrChannelField = null;
            DrResultField = null;

            dsRules = dsHandle.GetDataSet(@"Extractvalue(Column_Value, '/item/item_code') As item_code, Extractvalue(Column_Value, '/item/separated_first') As separated_first, 
                                                   Extractvalue(Column_Value, '/item/no_first') As no_first, Extractvalue(Column_Value, '/item/separated_second') As separated_second,
                                                   Extractvalue(Column_Value, '/item/no_second') As no_second, Extractvalue(Column_Value, '/item/start_bits') As start_bits,
                                                   Extractvalue(Column_Value, '/item/length') As length, Extractvalue(Column_Value, '/item/sign') As sign,Extractvalue(Column_Value, '/item/format') As format",
                                                   "Table(Xmlsequence(Extract((Select 解析规则 From 检验仪器 Where ID = '" + strInstrument_id + "'), '/root/item'))) ", "");
            //检验指标通道            
            //ds_ItemChannel = dsHandle.GetDataSet("通道编码,项目id,nvl(小数位数,2) as 小数位数,nvl(换算比,0) as 换算比", "仪器检测项目", "仪器id = '" + strInstrument_id + "'");
            tItemChannel = OracleHelper.GetDataTable(@"Select 通道编码, m.项目id, Nvl(小数位数, 2) As 小数位数, Nvl(换算比, 0) As 换算比, Nvl(加算值, 0) As 加算值, j.结果类型
                                        From 仪器检测项目 m, 检验项目 j
                                        Where m.项目id = j.项目id and m.仪器Id='" + StrDevice + "'");
            ds_ItemChannel.CaseSensitive = true;
            //检验图像通道
            //ds_GraphChannel = dsHandle.GetDataSet("CHANNEL_NO,GRAPH_TYPE", "TEST_GRAPH_CHANNEL", "instrument_id = '" + strInstrument_id + "'");
            //ds_GraphChannel.CaseSensitive = true;

            FindRow = dsRules.Tables[0].Select("item_code = '01'");         //检验日期标识
            if (FindRow.Length != 0) DrTestTimeSignField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '02'");             //检验日期
            if (FindRow.Length != 0) DrTestTimeField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '03'");       //常规样本号标识
            if (FindRow.Length != 0) DrSampleNoSignField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '04'");           //常规样本号
            if (FindRow.Length != 0) DrSampleNoField = FindRow[0];
            else
            {
                DrSampleNoField = null;
            }
            FindRow = dsRules.Tables[0].Select("item_code = '05'");      //质控样本号
            if (FindRow.Length != 0) DrQCSampleField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '06'");        //条码号标识
            if (FindRow.Length != 0) DrBarCodeSignField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '07'");        //条码号
            if (FindRow.Length != 0) DrBarCodeField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '08'");        //样本类型标识
            if (FindRow.Length != 0) DrSampleTypeSignField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '09'");        //样本类型
            if (FindRow.Length != 0) DrSampleTypeField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '10'");        //检验人标识
            if (FindRow.Length != 0) DrOperatorSignField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '11'");        //检验人
            if (FindRow.Length != 0) DrOperatorField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '12'");        //标本标识
            if (FindRow.Length != 0) DrSpecimenSignField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '13'");        //标本
            if (FindRow.Length != 0) DrSpecimenField = FindRow[0];

            FindRow = dsRules.Tables[0].Select("item_code = '14'");        //结果标识
            if (FindRow.Length != 0) DrResultSignField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '15'");        //结果信息
            if (FindRow.Length != 0) DrResultInfoField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '16'");        //结果数
            if (FindRow.Length != 0) DrResultCountField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '17'");      //单个结果
            if (FindRow.Length != 0) DrSingleResultField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '18'");        //通道号
            if (FindRow.Length != 0) DrChannelField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '19'");        //结果值
            if (FindRow.Length != 0) DrResultField = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '20'");        //盘号
            //if (FindRow.Length != 0) Field_Row[4] = FindRow[0];
            FindRow = dsRules.Tables[0].Select("item_code = '21'");        //杯号
            //if (FindRow.Length != 0) Field_Row[5] = FindRow[0];
            strDeviceID = StrDevice;
            strDevice = OracleHelper.GetDataTable("select 名称 from 检验仪器 where id='" + StrDevice + "'").Rows[0]["名称"].ToString();
        }

        public void SetVariable(System.Data.DataTable dt)
        {
            throw new NotImplementedException();
        }
    }
}
