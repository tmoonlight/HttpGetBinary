using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Windows.Forms;

namespace HttpGetBinary.Client.Winform
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                tbxFileName.Text = ofd.FileName;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

            string fileName = tbxFileName.Text;
            int maxLength = int.Parse(tbxMaxLength.Text);
            string url = tbxUrl.Text;

            byte[] bytes = new byte[maxLength];
            Get(url + "?t=S&f=" + HttpUtility.UrlEncode(Path.GetFileName(fileName)));
            int q = 0;
            using (var fileStream = File.OpenRead(fileName))
            {
                int readCount = 0;
                while ((readCount = fileStream.Read(bytes, 0, maxLength)) > 0)
                {
                    Console.WriteLine("发送" + readCount);
                    //string str = Convert.ToBase64String(bytes, 0, readCount);
                    string str = Base64Url.Encode(bytes,0,readCount); //HttpUtility.UrlEncode(bytes);

                    Get(url + "?t=" + str + "&q=" + q);
                    //var testbt = HttpUtility.UrlDecodeToBytes(str);
                   // Console.WriteLine(testbt.Length);
                    q++;
                }
            }
            Get(url + "?t=E");//结束

        }

        public static String Get(string url)
        {
            System.Net.HttpWebRequest request = System.Net.WebRequest.Create(url) as System.Net.HttpWebRequest;
            request.Method = "GET";
            //request.UserAgent = DefaultUserAgent;
            System.Net.HttpWebResponse result = request.GetResponse() as System.Net.HttpWebResponse;
            System.IO.StreamReader sr = new System.IO.StreamReader(result.GetResponseStream());
            string strResult = sr.ReadToEnd();
            sr.Close();
            //Console.WriteLine(strResult);
            return strResult;
        }
    }
}
