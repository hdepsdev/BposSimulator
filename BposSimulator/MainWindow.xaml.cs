using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Sockets;
using System.Net;
using System.Data;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace BposSimulator
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket clnt;
        private bool bIsConnected = false;

        public MainWindow()
        {
            try
            {
	            InitializeComponent();
	            clnt = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
	            //加载油品名称
	            string strOilCfg = ".\\OilType.xml";
	            DataSet oilTypeDs = new DataSet();
	            oilTypeDs.ReadXml(strOilCfg);
                DataView dv = oilTypeDs.Tables["OilType"].DefaultView;
                OilType.ItemsSource = dv;
                OilType.DisplayMemberPath = "name";
                OilType.SelectedValuePath = "id";
                //加载油品编号
                strOilCfg = ".\\OilId.xml";
                DataSet oilIdDs = new DataSet();
                oilIdDs.ReadXml(strOilCfg);
                dv = oilIdDs.Tables["OilId"].DefaultView;
                oilId.ItemsSource = dv;
                oilId.DisplayMemberPath = "name";
                oilId.SelectedValuePath = "id";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("找不到配置文件，请检查程序完整性!");
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try            
            {
                if (!bIsConnected)
                {
	                IPEndPoint iep = new IPEndPoint(IPAddress.Parse(Cfg.Default.serverIp), Cfg.Default.serverPort);
	                clnt.Connect(iep);
	                if (clnt.Connected)
	                {
                        bIsConnected = true;
                        btnConnect.Content = "关闭";
	                    btnSend.IsEnabled = true;
	                }
                } 
                else
                {
                    clnt.Disconnect(true);
                    bIsConnected = false;
                    btnConnect.Content = "连接";
                    btnSend.IsEnabled = false;
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("服务器出错.\r\n" + ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private byte[] stringToBcd(string src, int outLenth = -1)
        {
            byte[] dest;
            if (outLenth == -1)
            {
                //左对齐，右补F
	            if (src.Length%2 == 0)
	            {
	            	dest = new byte[src.Length/2];
	            } 
	            else
	            {
                    dest = new byte[src.Length / 2 + 1];
                    dest[src.Length / 2] |= 0x0f;
	            }

	            for (int i = 0; i < src.Length; i++)
	            {
	                if (i%2==0)
	                {
	                    dest[i/2] |= (byte)((src[i]-0x30)<<4);
	                } 
	                else
	                {
	                    dest[i/2] |= (byte)((src[i] - 0x30));
	                }
	            }
                return dest;
            } 
            else
            {
                //右对齐，左补0
                dest = new byte[outLenth];
                byte b = 0x00;
                for (int i = src.Length - 1, j = outLenth - 1; i >= 0; i--)
                {
                    if ((src.Length - i) % 2 == 0)
                    {
                        dest[j] |= (byte)((src[i] - 0x30) << 4);
                        b |= 0xF0;
                    }
                    else
                    {
                        dest[j] |= (byte)((src[i] - 0x30));
                        b |= 0x0F;
                    }
                    
                    if (b == 0xFF)
                    {
                        j--;
                        b = 0x00;
                    }
                }
                return dest;
            }
        }

        private string Bytes2String(byte[] src, int nLen)
        {
            string dest = "";

            for (int i = 0; i < nLen; i++)
            {
                //转换字节高位
                char c = (char)((src[i] & 0xF0) >> 4);
                if (c >= 0 && c <= 9)
                {
                    c += (char)0x30;
                }
                else
                {
                    c += (char)0x37;
                }
                dest += c;
                //转换字节低位
                c = (char)(src[i] & 0x0F);
                if (c >= 0 && c <= 9)
                {
                    c += (char)0x30;
                }
                else
                {
                    c += (char)0x37;
                }
                dest += c;
            }
            return dest;
        }

        private byte[] SeBytesToBeBytes(byte[] src)
        {
            byte[] dest = new byte[src.Count()];
            for (int i = 0; i < src.Length; i++)
            {
                dest[src.Length - 1 - i] = src[i];
            }

            return dest;
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            //tpdu header
            byte[] tpduHeader = new byte[10];
            //sync
            tpduHeader[0] = 0x10;
            tpduHeader[1] = 0x10;
            //type
            tpduHeader[6] = 0x01;
            //pkgNum
            tpduHeader[7] = 0x00;
            tpduHeader[8] = 0x00;
            //crc8
            tpduHeader[9] = 0x00;

            //tpdu body
            //bizHeader
            byte[] bizHeader = new byte[8];
            //station
            System.Array.Copy(stringToBcd(Cfg.Default.stationId), 0, bizHeader, 0, 5);
            //casher
            System.Array.Copy(System.BitConverter.GetBytes(Cfg.Default.casherId), 0, bizHeader, 5, 2);
            //cmd
            bizHeader[7] = 0x70;
            //bizBody
            byte[] bizBody = new byte[36];
            //oil gun id
            byte[] oidGunId = SeBytesToBeBytes(System.BitConverter.GetBytes(int.Parse(txtOilGun.Text.Trim())));
            System.Array.Copy(oidGunId, bizBody, 4);
            //payment id
//            RandomNumberGenerator randgen = new RNGCryptoServiceProvider(); 
            //byte[] random = new byte[4];
//            randgen.GetBytes(random);
            Random rd = new Random();
            int rs = rd.Next(100000000);
            string time = DateTime.Now.ToString("yyyyMMddHHmmss");
            System.Array.Copy(stringToBcd(time), 0, bizBody, 4, 7);
            System.Array.Copy(SeBytesToBeBytes(stringToBcd(rs.ToString(), 4)), 0, bizBody, 11, 4);
            //oilType
            bizBody[15] = stringToBcd(OilType.SelectedValue.ToString().Substring(2))[0];
            //oilid
            bizBody[16] = stringToBcd(oilId.SelectedValue.ToString().Substring(2))[0];
            //price
            int price = (int)(decimal.Parse(oilPrice.Text.Trim()) * 100);
            System.Array.Copy(stringToBcd(price.ToString(), 5), 0, bizBody, 17, 5);
            //amount
            int amount = (int)(decimal.Parse(oilAmount.Text.Trim()) * 100);
            System.Array.Copy(stringToBcd(amount.ToString(), 6), 0, bizBody, 22, 6);
            //count
            int count = (int)(decimal.Parse(oilCount.Text.Trim()) * 100);
            System.Array.Copy(stringToBcd(count.ToString(), 8), 0, bizBody, 28, 8);
            //tpdubody
            byte[] tpduBody = new byte[bizHeader.Count() + bizBody.Count() + 4];
            System.Array.Copy(bizHeader, tpduBody, bizHeader.Count());
            System.Array.Copy(bizBody, 0, tpduBody, 8, bizBody.Count());
            System.Array.Copy(stringToBcd(Cfg.Default.Mac), 0, tpduBody, bizHeader.Count() + bizBody.Count(), 4);
            //tpdu header length
            Int32 len = tpduBody.Count();
            byte[] tpdulens = SeBytesToBeBytes(System.BitConverter.GetBytes(len));
            System.Array.Copy(tpdulens, 0, tpduHeader, 2, 4);
            //tpdu
            byte[] tpdu = new byte[tpduHeader.Count() + tpduBody.Count()];
            System.Array.Copy(tpduHeader, tpdu, tpduHeader.Count());
            System.Array.Copy(tpduBody, 0, tpdu, tpduHeader.Count(), tpduBody.Count());

            //send
            //发送指定数据
            clnt.Send(tpdu);
            txtDataRecved.Text += "Send:" + Bytes2String(tpdu, tpdu.Length) + "\r\n";
            //同步接收数据
            byte[] recvBuf = new byte[256];
            int nLen = clnt.Receive(recvBuf);
            txtDataRecved.Text += "Recv:" + Bytes2String(recvBuf, nLen) + "\r\n";
        }

        private void txtOilGun_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex("[^0-9]+");
            e.Handled = re.IsMatch(e.Text);
        }

        private void decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex("[^0-9.]+");
            e.Handled = re.IsMatch(e.Text);
        }

        private void decimal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (oilPrice.Text.Trim().Count() > 0 && oilAmount.Text.Trim().Count() > 0)
            {
	            decimal count = decimal.Parse(oilPrice.Text.Trim()) * decimal.Parse(oilAmount.Text.Trim());
	            oilCount.Text = count.ToString("#0.00");
            }
        }
    }
}
