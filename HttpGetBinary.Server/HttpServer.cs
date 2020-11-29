using System;
using System.Buffers.Text;
using System.Collections.Generic;
//using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;


// ReSharper disable All

namespace HttpGetBinary.Server
{
    public partial class HttpServer
    {
        #region HTTPServer



        private const string INDEX_PAGE = "/main.html";
        private const string BASE_FILE_PATH = "./Web/";
        //private const string BASE_LOG_FILE_PATH = "./log";

        public Dictionary<string, MemoryStream> FilesCache = new Dictionary<string, MemoryStream>(20);


        public HttpServer()
        {

            //第一次加载所有mime类型

        }



        /// <summary>
        /// http服务启动，初始化代码写在这里
        /// </summary>
        /// <param name="ctsHttp"></param>
        /// <param name="WebManagementPort"></param>
        /// <returns></returns>
        public async Task StartHttpService(CancellationTokenSource ctsHttp, int WebManagementPort)
        {
            try
            {
                HttpListener listener = new HttpListener();
                //缓存所有文件
                //var dir = new DirectoryInfo(BASE_FILE_PATH);
                //var files = dir.GetFiles("*.*");
                //foreach (var file in files)
                //{
                //    using (var fs = file.OpenRead())
                //    {
                //        var mms = new MemoryStream();
                //        fs.CopyTo(mms);
                //        FilesCache.Add(file.Name, mms);
                //    }
                //}
                //LoggerDebug($"{files.Length} files cached.");

                listener.Prefixes.Add($"http://+:{WebManagementPort}/");
                LoggerDebug("Listening HTTP request on port " + WebManagementPort.ToString() + "...");
                await AcceptHttpRequest(listener, ctsHttp);
            }
            catch (HttpListenerException ex)
            {
                LoggerDebug("Please run this program in administrator mode." + ex);
                LoggerError(ex.ToString(), ex);
            }
            catch (Exception ex)
            {
                LoggerDebug(ex.ToString());
                LoggerError(ex.ToString(), ex);
            }
            LoggerDebug("Http服务结束。");
        }

        private void LoggerDebug(string str)
        {
            Console.WriteLine(str);
        }

        private async Task AcceptHttpRequest(HttpListener httpService, CancellationTokenSource ctsHttp)
        {
            httpService.Start();
            while (true)
            {
                var client = await httpService.GetContextAsync();
                _ = ProcessHttpRequestAsync(client);
            }
        }

        //需要确保顺序
        FileStream fs = null;

        private async Task ProcessHttpRequestAsync(HttpListenerContext context)
        {

            var request = context.Request;
            var response = context.Response;
            //context上下文设置给WebContext
            //TODO XX 设置该同源策略为了方便调试，真实项目请确保同源

#if DEBUG
            response.AddHeader("Access-Control-Allow-Origin", "*");
#endif
            response.Headers["Server"] = "";
            try
            {
                //通过request来的值进行接口调用
                string unit = request.RawUrl.Replace("//", "");

                if (unit == "/") unit = INDEX_PAGE;

                int idx1 = unit.LastIndexOf("#");
                if (idx1 > 0) unit = unit.Substring(0, idx1);
                int idx2 = unit.LastIndexOf("?");
                if (idx2 > 0) unit = unit.Substring(0, idx2);
                int idx3 = unit.LastIndexOf(".");

                //通过后缀获取不同的文件，若无后缀，则调用接口
                if (idx3 > 0)
                {

                    if (!File.Exists(BASE_FILE_PATH + unit))
                    {
                        LoggerDebug($"未找到文件{BASE_FILE_PATH + unit}");
                        return;

                    }
                    //mime类型
                    //ProcessMIME(response, unit.Substring(idx3));
                    //TODO 权限控制（只是控制html权限而已）

                    //读文件优先去缓存读
                    if (FilesCache.TryGetValue(unit.TrimStart('/'), out MemoryStream memoryStream))
                    {
                        memoryStream.Position = 0;
                        await memoryStream.CopyToAsync(response.OutputStream);
                    }
                    else
                    {
                        using (FileStream fs = new FileStream(BASE_FILE_PATH + unit, FileMode.Open))
                        {
                            await fs.CopyToAsync(response.OutputStream);
                        }
                    }
                }
                else  //url中没有小数点则是接口
                {
                    unit = unit.Replace("/", "");
                    response.ContentEncoding = Encoding.UTF8;

                    //调用接口 用分布类隔离并且用API特性限定安全
                    object jsonObj;
                    //List<string> qsStrList;
                    int qsCount = request.QueryString.Count;
                    object[] parameters = null;
                    if (qsCount > 0)
                    {
                        parameters = new object[request.QueryString.Count];
                        for (int i = 0; i < request.QueryString.Count; i++)
                        {
                            parameters[i] = request.QueryString[i];
                        }
                    }

                    string basesource = parameters[0].ToString();
                    //string srcString = HttpUtility.UrlDecode(basesource);
                   

                    
                    //需要考虑接收顺序

                    if (basesource == "S")//开始写文件
                    {
                        var targetFileName = request.QueryString["f"].ToString();
                        LoggerDebug("开始写文件" + targetFileName);
                        fs = new FileStream(targetFileName, FileMode.Create); //File.OpenWrite(targetFileName);
                    }
                    else if (basesource == "E")//终止写文件
                    {
                        fs.Close();
                        LoggerDebug("终止写文件");
                    }
                    else
                    {
                        var srcBytes = Base64Url.Decode(basesource);
                        LoggerDebug("接收到字符长度：" + srcBytes.Length);
                        fs.Write(srcBytes);
                        LoggerDebug("序列" + request.QueryString["q"].ToString());
                    }


                }

            }
            catch (Exception e)
            {
                LoggerError(e.Message, e);
                throw;
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        private void LoggerError(string exMessage, Exception p1)
        {
            Console.WriteLine(exMessage, p1.ToString());
        }



        #region 文件读取
        private String GetBoundary(String ctype)
        {
            return "--" + ctype.Split(';')[1].Split('=')[1];
        }

        private string SaveFile(Encoding enc, String contentType, Stream input)
        {

            Byte[] boundaryBytes = enc.GetBytes(GetBoundary(contentType));
            Int32 boundaryLen = boundaryBytes.Length;
            string fileName = Guid.NewGuid().ToString("N") + ".temp";
            using (FileStream output = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                Byte[] buffer = new Byte[1024];
                Int32 len = input.Read(buffer, 0, 1024);
                Int32 startPos = -1;

                // Find start boundary
                while (true)
                {
                    if (len == 0)
                    {
                        throw new Exception("Start Boundaray Not Found");
                    }

                    startPos = IndexOf(buffer, len, boundaryBytes);
                    if (startPos >= 0)
                    {
                        break;
                    }
                    else
                    {
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = input.Read(buffer, boundaryLen, 1024 - boundaryLen);
                    }
                }

                // Skip four lines (Boundary, Content-Disposition, Content-Type, and a blank)
                for (Int32 i = 0; i < 4; i++)
                {
                    while (true)
                    {
                        if (len == 0)
                        {
                            throw new Exception("Preamble not Found.");
                        }

                        startPos = Array.IndexOf(buffer, enc.GetBytes("\n")[0], startPos);
                        if (startPos >= 0)
                        {
                            startPos++;
                            break;
                        }
                        else
                        {
                            len = input.Read(buffer, 0, 1024);
                        }
                    }
                }

                Array.Copy(buffer, startPos, buffer, 0, len - startPos);
                len = len - startPos;

                while (true)
                {
                    Int32 endPos = IndexOf(buffer, len, boundaryBytes);
                    if (endPos >= 0)
                    {
                        if (endPos > 0) output.Write(buffer, 0, endPos - 2);
                        break;
                    }
                    else if (len <= boundaryLen)
                    {
                        throw new Exception("End Boundaray Not Found");
                    }
                    else
                    {
                        output.Write(buffer, 0, len - boundaryLen);
                        //每次放置后40个字节到首部，读取接下来984个字节，在此基础上进行byte查找，绝妙！
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = input.Read(buffer, boundaryLen, 1024 - boundaryLen) + boundaryLen;
                    }
                }
            }

            return fileName;
        }

        private Int32 IndexOf(Byte[] buffer, Int32 len, Byte[] boundaryBytes)
        {
            for (Int32 i = 0; i <= len - boundaryBytes.Length; i++)
            {
                Boolean match = true;
                for (Int32 j = 0; j < boundaryBytes.Length && match; j++)
                {
                    match = buffer[i + j] == boundaryBytes[j];
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
        #endregion

        #endregion

    }
}