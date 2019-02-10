using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;


namespace YuneecGoogleEarth
{
    public partial class Form1 : Form
    {
        private const int QuadAddress = 0x8EE2;
        private const int ControllerAddress = 0x529A;

        private readonly KmlWriter.KmlWriter _kmlWriter;
        private readonly YuneecDecoder _yuneecDecoder;
        private int _countC;
        private int _countQ;
        private bool _threadRunning;
        private UdpClient _udpServer;
        private Thread _updThread;

        //Lat Lon Alt list of points
        private readonly List<Tuple<double, double, double>> _coordinateList = new List<Tuple<double, double, double>>();

        private StreamWriter _logFile;
        private float _offset;

        public Form1()
        {
            InitializeComponent();
            _yuneecDecoder = new YuneecDecoder();
            _kmlWriter = new KmlWriter.KmlWriter();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _logFile = new StreamWriter($"Log_{DateTime.Now.ToString("s").Replace(':', '-')}.csv");
            label1.Text = @"RxQ:0 RxC:0";
            timer1.Start();
            timer2.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _logFile.Close();
            _threadRunning = false;
            _udpServer?.Close();
            _updThread?.Abort();
        }

        // Runs on its own thread to get UDP packets from the Texas Instrument broadcaster
        private void UpdClientThread()
        {
            _udpServer = new UdpClient(5000);
            _threadRunning = true;
            while (_threadRunning)
            {
                try
                {
                    var remoteEp = new IPEndPoint(IPAddress.Any, 1000);

                    var data = _udpServer.Receive(ref remoteEp);

                    if (ZigBeeDecoder.PacketSnifferHeader.TryParse(data, out var psh) == false)
                    {
                        return;
                    }

                    if (ZigBeeDecoder.ZigBeeHeader.TryParse(data, out var header, ZigBeeDecoder.PacketSnifferHeader.Size) == false)
                    {
                        return;
                    }

                    var subData = new byte[data.Length - ZigBeeDecoder.PacketSnifferHeader.Size];

                    Array.Copy(data, ZigBeeDecoder.PacketSnifferHeader.Size,
                        subData, 0, subData.Length);

                    _yuneecDecoder.DecodeMessage(subData, header);
                    if (header.Src == ControllerAddress)
                    {
                        _countC++;
                    }

                    if (header.Src == QuadAddress)
                    {
                        _countQ++;
                    }

                    //var message = ZigBeeDecoder.Conversion.ByteArrayToString(data, ZigBeeDecoder.PacketSnifferHeader.Size);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

       

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (_threadRunning)
            {
                buttonConnect.Text = @"Connect";
                _threadRunning = false;
                _udpServer.Close();
                _updThread.Abort();
            }
            else
            {
                if (!_yuneecDecoder.LoadSettings())
                {
                    return;
                }

                buttonConnect.Text = @"Disconnect";
                _threadRunning = true;
                _updThread = new Thread(UpdClientThread);
                _updThread.Start();
            }
        }

        private void UpdateChartSeries(ushort srcAdd, string valueName, string seriesName)
        {
            if (!_yuneecDecoder.GetValue(srcAdd, valueName, out var v))
            {
                return;
            }

            var p = chart1.Series[seriesName].Points;
            p.AddY(v);
            if (p.Count > 10)
            {
                p.RemoveAt(0);
            }
        }

        #region Timers
        private void timer1_Tick(object sender, EventArgs e)
        {
            label1.Text = $@"RxQ:{_countQ} RxC:{_countC}";

            //From Quad
            UpdateChartSeries(QuadAddress, "roll", "Roll");
            UpdateChartSeries(QuadAddress, "pitch", "Pitch");

            //From Controller
            UpdateChartSeries(ControllerAddress, "Up Down Control and start", "UpDown");
            UpdateChartSeries(ControllerAddress, "Left Right Control", "LeftRight");
            UpdateChartSeries(ControllerAddress, "Forward Reverse Control", "FwdRev");
            UpdateChartSeries(ControllerAddress, "Rotation left right Control", "Rotate");
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (!_yuneecDecoder.GetValue(QuadAddress, "Latitude", out var lat) || !_yuneecDecoder.GetValue(QuadAddress, "Longitude", out var lon) ||
                !_yuneecDecoder.GetValue(QuadAddress, "Altitude", out var alt) || !_yuneecDecoder.GetValue(QuadAddress, "Voltage", out var voltage))
            {
                return;
            }

            //Bail on bad messages
            if (lat > 90 || lat < -90 ||
                lon > 180 || lon < -180 ||
                alt > 1000)
            {
                return;
            }

            alt -= _offset;

            _logFile.WriteLine($"{lat},{lon},{alt},{_offset},{voltage}");
            _logFile.Flush();

            _kmlWriter.UpdateKmlVehicle(lat, lon, alt, 0, voltage.ToString(CultureInfo.CurrentCulture), "Q500");

            _coordinateList.Add(new Tuple<double, double, double>(lat, lon, alt));
            if (_coordinateList.Count > 100)
            {
                _coordinateList.RemoveAt(0);
            }

            _kmlWriter.UpdateKmlPath(_coordinateList, "Q500");
        }
        #endregion

        private void buttonOffset_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show(@"Input Offset or select ""Altitude"" button first.");
                return;
            }

            //Take last altitude and offset the rest
            if (float.TryParse(textBox1.Text, out var offsetInput))
            {
                _offset = offsetInput;
            }
            else
            {
                MessageBox.Show(@"Invalid offset.");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (_yuneecDecoder.GetValue(QuadAddress, "Altitude", out var alt))
            {
                textBox1.Text = alt.ToString("F2");
            }
        }
    }
}