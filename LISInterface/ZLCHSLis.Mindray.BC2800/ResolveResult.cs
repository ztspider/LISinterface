﻿/*************************************
 * 名称：BC2800
 * 功能：血常规
 * 作者：谢天
 * 时间：2017-3-8
 * 通讯类型：串口
 * COM1 波特率:9600  数据位:8   停止位:1   校验:无    控制流:无 
 * 备注:无需应答字符串
 * 
首先要修改通讯协议  修改通讯协议密码  3210   普通用户权限1800

选择通讯协议为3005(15 ID)  其他的协议   3001 8  id  3005 10  id
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
using System.Data.OracleClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ZLCHSLis.Mindray.BC2800
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
        public List<string> listInputResult = new List<string>();
        public List<string> ListImagePosition = new List<string>();              //图像存放位置
        public Boolean ImmediatelyUpdate = false;                               //立即更新

        DataSetHandle dsHandle = new DataSetHandle();
        Write_Log writelog = new Write_Log();
        SaveResult saveResult;
        string TestResultValue;      //解析后的通用字符串
        string strDevice;
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

        string startStr;//仪器发送开始符号
        string endStr;//仪器发送结束符号
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
        public void ParseResult()
        {
            throw new NotImplementedException();
        }
        private int IndexOfN(string str, string match, int n)
        {
            string str1 = str;
            string s = "";
            string s1 = "";
            string Tag = "TT-TT-TT-TT";
            int returnValue = 0;
            for (int i = 0; i < n; i++)
            {

                s = str1.Substring(0, str1.IndexOf(match) + match.Length);
                s1 = str1.Substring(str1.IndexOf(match) + match.Length, str1.Length - s.Length);
                s = s.Replace(match, Tag);
                s = s + s1;
                str1 = s;
                returnValue = (match.Length - Tag.Length) * n + s.IndexOf(match);
            }
            return returnValue;

        }
        public void ParseResult(string strSource, ref string strResult, ref string strReserved, ref string strCmd)
        {
            // 
            char c08 = Convert.ToChar(0x08);
            char c04 = Convert.ToChar(0x04);
            char coc = Convert.ToChar(0x0C);
            char c16 = Convert.ToChar(0x16);
            char c0a = Convert.ToChar(0x0A);
            char c0f = Convert.ToChar(0x0F);
            string endstring = c0a + "" + c0f;
            string endstring1 = c0a + "" + c0f + "" + c0f;
            if (strSource.EndsWith(endstring) || strSource.EndsWith(endstring1))
            {
                strResult += strSource;
                try
                {
                    bool isSampleBar = true;
                    //将接收到的字符串写入到日志文件中
                    writelog.Write(strDevice, "接收数据开始：\r\n " + strResult, "log");
                    writelog.Write(strDevice, "接收数据完成 ", "log");

                    StreamReader sr = new StreamReader(@".\Mindray.BC2800.txt");
                    string correspondence = sr.ReadLine();
                    sr.Close();
                    string[] correspondenceArray = correspondence.Split('|');
                    StringBuilder sBuilder = new StringBuilder("");


                    string[] ss = strResult.Split(new char[] { c04 }, StringSplitOptions.RemoveEmptyEntries);
                    string ChannelCode = "";
                    for (int i = 0; i < ss.Length; i++)
                    {

                        if (ss[i].Trim().IndexOf("SampleID") != -1)
                        {

                            string sTemp = ss[i].Trim();

                            strSampleNo = sTemp.Substring(sTemp.IndexOf("SampleID") + 9, sTemp.IndexOf(c08, 0) - sTemp.IndexOf("SampleID") - 9);
                            strTestTime = sTemp.Substring(sTemp.IndexOf("TestTime") + 9, this.IndexOfN(sTemp, c08 + "", 2) - sTemp.IndexOf("TestTime") - 9);


                        }
                        for (int j = 0; j < correspondenceArray.Length; j++)
                        {
                            string sTemp = ss[i].Trim();
                            string lis = correspondenceArray[j].Split(',')[0];
                            string zlchs = correspondenceArray[j].Split(',')[1];
                            if (ss[i].StartsWith(lis + coc))
                            {
                                string temp = sTemp.Substring(sTemp.IndexOf("Val" + c16) + 4, sTemp.IndexOf(c08, 0) - sTemp.IndexOf("Val" + c16) - 4);
                                sBuilder.Append(zlchs + ',' + temp + '|');
                            }
                        }
                    }

                    string str = sBuilder.ToString().Remove(sBuilder.Length - 1, 1);
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
                            TestResultValue = TestResultValue + testItemID + "^" + strs[i].Split(',')[1] + "|";
                        }
                    }
                    TestResultValue = strTestTime + "|" + strSampleNo + "^" + strSampleType + "^" + strBarCode + "|" + strOperator + "|" + StrSpecimen + "|" + "|" + TestResultValue;
                    saveResult = new SaveResult();
                    if (!string.IsNullOrEmpty(strSampleNo))
                    {
                        //strInstrument_id 替换到目标设备
                        if (string.IsNullOrEmpty(strReserved))
                            saveResult.SaveTextResult(strInstrument_id, TestResultValue, TestGraph, DrSampleNoField);
                        else saveResult.SaveTextResult(strReserved, TestResultValue, TestGraph, DrSampleNoField);
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
                }
                strResult = "";
            }
            else
            {
                strResult += strSource;
            }
            strCmd = Convert.ToChar(0x06) + "";
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

            strDevice = OracleHelper.GetDataTable("select 名称 from 检验仪器 where id='" + StrDevice + "'").Rows[0]["名称"].ToString();
        }

        public void SetVariable(System.Data.DataTable dt)
        {
            throw new NotImplementedException();
        }
    }
}
