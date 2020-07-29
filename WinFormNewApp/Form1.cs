using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using System.Collections;
using System.IO;
using NReco.Csv;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;

namespace WinFormNewApp
{
    public partial class Form1 : Form
    {
        // 变量区
        const int frm_len = 12;          // 每帧数据长度
        const int channel_num = 6;       // 采样通道
        const int page_max_num = 100;    // 最大页数

        float text_box_font_size = 9.0f;  // 字体大小
        private const int queue_cnt = 6000; // 每个采样通道最大数据量
        private bool paint_browse_mode = true;  // 默认绘图模式
        private int total_frms_last = 0;  // 上一次帧次数
        private int page_cnt = 0;  // 页数
        private int current_page = 0;  // 页数
        private int save_index = 0;  // 保存数据索引


        // 使用交错数组实现，以下数组保存历史数据
        int[][] JaggedArrayCh0 = new int[page_max_num][];  // 100页
        int[][] JaggedArrayCh1 = new int[page_max_num][];  // 100页
        int[][] JaggedArrayCh2 = new int[page_max_num][];  // 100页
        int[][] JaggedArrayCh3 = new int[page_max_num][];  // 100页
        int[][] JaggedArrayCh4 = new int[page_max_num][];  // 100页
        int[][] JaggedArrayCh5 = new int[page_max_num][];  // 100页

        // my code start here
        static object apple = new object(); // 创建1个互斥体

        // 以下队列用于绘图
        private Queue<int> serialDataCh0Queue = new Queue<int>();
        private Queue<int> serialDataCh1Queue = new Queue<int>();
        private Queue<int> serialDataCh2Queue = new Queue<int>();
        private Queue<int> serialDataCh3Queue = new Queue<int>();
        private Queue<int> serialDataCh4Queue = new Queue<int>();
        private Queue<int> serialDataCh5Queue = new Queue<int>();

        private Thread paint_thread = null;  // 绘图线程
        private Thread textBox_thread = null;  // 绘图线程

        ClassMySerial mySerial = new ClassMySerial();

        public Form1()
        {
            InitializeComponent();
            Chart1Init();
            myControlsInit();   // 控件初始化

            mySerial.find_available_ports();

            // 将可用串口添加到列表中
            foreach (string port in mySerial.ports)
            {
                comboBox1.Items.Add(port);
            }
            if (mySerial.ports.Length >= 1)
            {
                comboBox1.Text = mySerial.ports[0];
                serialPort1.PortName = comboBox1.Text;      // 串口号
                serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);     // 波特率

                serialPort1.ReceivedBytesThreshold = 24;  // 每12个字节触发一次事件
                serialPort1.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                toolStripStatusLabel2.Text = "串口未打开";
                toolStripStatusLabel2.BackColor = Color.Gray;
            }

            paint_thread = new Thread(new ThreadStart(paint_task)); //创建线程                     

        }

        private void myControlsInit()
        {
            //获取应用程序的当前工作目录
            string app_path = System.IO.Directory.GetCurrentDirectory();

            // 读取配置文件
            app_path = app_path + @"\app.ini";
            IniFile ini = new IniFile(app_path);
            string PortName, BaudRate;

            PortName = ini.IniReadValue("serialport", "PortName");
            BaudRate = ini.IniReadValue("serialport", "BaudRate");

            // 判断是否有配置文件
            if (PortName.Length == 0)
            {
                MessageBox.Show("当前目录下无配置文件，建议添加");
                comboBox2.SelectedIndex = 0;
            }
            else
            {
                comboBox1.Text = PortName;
                comboBox2.Text = BaudRate;
                chart1.Series[0].LegendText = ini.IniReadValue("channelName", "ch0");
                chart1.Series[1].LegendText = ini.IniReadValue("channelName", "ch1");
                chart1.Series[2].LegendText = ini.IniReadValue("channelName", "ch2");
                chart1.Series[3].LegendText = ini.IniReadValue("channelName", "ch3");
                chart1.Series[4].LegendText = ini.IniReadValue("channelName", "ch4");
                chart1.Series[5].LegendText = ini.IniReadValue("channelName", "ch5");

                checkBox3.Text = chart1.Series[0].LegendText;
                checkBox4.Text = chart1.Series[1].LegendText;
                checkBox5.Text = chart1.Series[2].LegendText;
                checkBox6.Text = chart1.Series[3].LegendText;
                checkBox7.Text = chart1.Series[4].LegendText;
                checkBox8.Text = chart1.Series[5].LegendText;
            }

            // 将配置信息写入到控件中

            // 绘图通道选择
            checkBox3.Checked = true;     // 默认显示CH0
            checkBox4.Checked = true;     // 默认显示CH1
            checkBox5.Checked = true;     // 默认显示CH2
            checkBox6.Checked = true;     // 默认显示CH3
            checkBox7.Checked = true;     // 默认显示CH4
            checkBox8.Checked = true;     // 默认显示CH5
            radioButton1.Checked = true;

        }

        private void Chart1Init()
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            cursorX_Y_zoom();
        }

        // X轴与Y轴放大、缩小功能实现
        private void cursorX_Y_zoom()
        {
            if (checkBox1.Checked)
            {
                chart1.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            }
            else
            {
                chart1.ChartAreas[0].CursorX.IsUserSelectionEnabled = false;
            }

            if ((checkBox10.Checked == false) && (checkBox10.Checked == false))
            {
                chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = false;
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked == true)
            {
                checkBox10.Checked = false;
                chart1.ChartAreas[0].CursorY.AxisType = (AxisType)0;  // 0为主轴
                chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
            }
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox10.Checked == true)
            {
                checkBox2.Checked = false;  // 二选一
                chart1.ChartAreas[0].CursorY.AxisType = (AxisType)1;  // 1为辅助轴
                chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
            }
            if ((checkBox10.Checked == false) && (checkBox10.Checked == false))
            {
                chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = false;
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            display_value();
        }

        private void display_value()
        {
            if (chart1.ChartAreas[0].AxisX.ScaleView.ViewMaximum - chart1.ChartAreas[0].AxisX.ScaleView.ViewMinimum <= 50)
            {
                for (int i = 0; i < 6; i++)
                {
                    chart1.Series[i].IsValueShownAsLabel = true;            
                }
                setAxisMarkerStyle(true);
            }
            else
            {
                checkBox9.Checked = false;
                MessageBox.Show("X轴个数太多，无法清晰显示!");
            }
        }

        private void setAxisMarkerStyle(bool flag)
        {
            if (true == flag)
            {
                for (int i = 0; i < 6; i++)
                {
                    chart1.Series[i].MarkerStyle = MarkerStyle.Square;
                }
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    chart1.Series[i].MarkerStyle = MarkerStyle.None;
                }
            }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            checkBox9.Checked = false;  // 先取消值的显示
            setAxisMarkerStyle(false);
            chart1.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);//ZoomReset(0)表示撤销所有放大动作
            chart1.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);//ZoomReset(1)表示撤销上一次放大动作
            chart1.ChartAreas[0].AxisY2.ScaleView.ZoomReset(0);

        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                chart1.Series[0].Enabled = true;
            }
            else
            {
                chart1.Series[0].Enabled = false;
            }
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox4.Checked)
            {
                chart1.Series[1].Enabled = true;
            }
            else
            {
                chart1.Series[1].Enabled = false;
            }
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox5.Checked)
            {
                chart1.Series[2].Enabled = true;
            }
            else
            {
                chart1.Series[2].Enabled = false;
            }
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox6.Checked)
            {
                chart1.Series[3].Enabled = true;
            }
            else
            {
                chart1.Series[3].Enabled = false;
            }
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox7.Checked)
            {
                chart1.Series[4].Enabled = true;
            }
            else
            {
                chart1.Series[4].Enabled = false;
            }
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox8.Checked)
            {
                chart1.Series[5].Enabled = true;
            }
            else
            {
                chart1.Series[5].Enabled = false;
            }
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {

        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            if(checkBox9.Checked)
            {
                display_value();
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    chart1.Series[i].IsValueShownAsLabel = false;
                }
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            comboBox1.Text = "";
            comboBox1.Items.Clear();
            mySerial.find_available_ports();
            // 将可用串口添加到列表中
            foreach (string port in mySerial.ports)
            {
                comboBox1.Items.Add(port);
            }

            if (mySerial.ports.Length >= 1)
            {
                comboBox1.Text = mySerial.ports[0];
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // 先判断串口号是否存在
            if (comboBox1.Text == "")
            {
                MessageBox.Show("串口号不存在！");
            }
            else
            {
                if (serialPort1.IsOpen != true)
                {
                    // 尝试打开
                    try
                    {
                        serialPort1.PortName = comboBox1.Text;      // 串口号
                        serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);     // 波特率
                        serialPort1.Open();
                        serialPort1.DiscardInBuffer();  // 清一次缓存数据

                        paint_thread = new Thread(new ThreadStart(paint_task)); //创建线程                     

                        paint_thread.Start(); //启动线程
                    }
                    catch (System.IO.IOException)
                    {
                        MessageBox.Show("串口打开失败！");
                    }
                    catch (System.UnauthorizedAccessException)
                    {
                        MessageBox.Show("串口访问被拒绝！");
                    }
                    finally
                    {
                        // 状态提示
                        if (serialPort1.IsOpen == true)
                        {
                            toolStripStatusLabel2.Text = "串口已打开";
                            toolStripStatusLabel2.BackColor = Color.Green;
                            button5.Text = "关闭";
                        }
                        else
                        {
                            ;
                        }
                    }
                }
                else
                {
                    // 关闭动作
                    serialPort1.Close();
                    timer2.Stop();
                    button5.Text = "打开";
                    toolStripStatusLabel2.Text = "串口未打开";
                    toolStripStatusLabel2.BackColor = Color.Gray;
                }
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            // 清空缓冲区
            textBox1.Text = "";
            if (serialPort1.IsOpen == true)
            {
                //serialPort1.DiscardInBuffer();
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < channel_num; i++)
            {
                chart1.Series[i].Points.Clear();
            }

            if (serialPort1.IsOpen == true)
            {
                serialPort1.DiscardInBuffer();
            }
            // 清除队列值
            serialDataCh0Queue.Clear();
            serialDataCh1Queue.Clear();
            serialDataCh2Queue.Clear();
            serialDataCh3Queue.Clear();
            serialDataCh4Queue.Clear();
            serialDataCh5Queue.Clear();

            // 切换到绘图模式
            radioButton1.Checked = true;
        }


        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            int tmp = 0;
            int total_bytes = sp.BytesToRead;

            Console.WriteLine(total_bytes);
            //标准帧数据
            byte[] buf_serial = new byte[10];  // 串口缓冲区

            // 读取1个字节
            tmp = sp.ReadByte();
            if (tmp == 0xAA)
            {
                tmp = sp.ReadByte();
                if (tmp == 0xAA)  // 帧头校验
                {
                    sp.Read(buf_serial, 0, 10);  // 读取后续10个字节
                    if (buf_serial[9] == 0xFE)  // 帧尾校验
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            // 有效帧处理
                            lock (apple)
                            {
                                switch (i % 10)
                                {
                                    case 0:
                                        serialDataCh0Queue.Enqueue(buf_serial[i]);
                                        break;
                                    case 1:
                                        serialDataCh1Queue.Enqueue(buf_serial[i]);
                                        break;
                                    case 2:
                                        serialDataCh2Queue.Enqueue(buf_serial[i]);
                                        break;
                                    case 3:
                                        tmp = buf_serial[i];
                                        break;
                                    case 4:
                                        serialDataCh3Queue.Enqueue((tmp << 8) + buf_serial[i]);
                                        break;
                                    case 5:
                                        tmp = buf_serial[i];
                                        break;
                                    case 6:
                                        serialDataCh4Queue.Enqueue((tmp << 8) + buf_serial[i]);
                                        break;
                                    case 7:
                                        tmp = buf_serial[i];
                                        break;
                                    case 8:
                                        serialDataCh5Queue.Enqueue((tmp << 8) + buf_serial[i]);
                                        break;
                                    case 9:
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }

                    }                  
                }
            }
            else
            {
                ;
            }
        }

        private void parse_serial_raw_data()
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            text_box_font_size++;
            textBox1.Font = new Font(textBox1.Font.FontFamily, text_box_font_size, textBox1.Font.Style);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            text_box_font_size--;
            textBox1.Font = new Font(textBox1.Font.FontFamily, text_box_font_size, textBox1.Font.Style);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            paint_thread = new Thread(new ThreadStart(paint_task));
            paint_thread.Start();
        }

        // 绘图任务
        private void paint_task()
        {
            while (true)
            {
                if (paint_thread.IsAlive)
                {
                    this.Invoke(new EventHandler(delegate
                    {
                        paint();
                    }));
                }
                Thread.Sleep(200);
            }
        }
        private void textBox_task()
        {

            while (true)
            {
                //lock(apple)
                {
                    this.Invoke(new EventHandler(delegate
                    {
                        for (int i = 0; i < 50; i++)
                        {

                        }
                    }));
                }

                Thread.Sleep(50);
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (paint_thread == null)
            {
                ;  // 无需动作
            }
            else
            {
                if (paint_thread.IsAlive)
                {
                    paint_thread.Abort();
                }
            }

            if (textBox_thread == null)
            {
                ;  // 无需动作
            }
            else
            {
                if (textBox_thread.IsAlive)
                {
                    textBox_thread.Abort();
                }
            }

        }

        // 绘图函数
        private void paint()
        {
            lock (apple)
            {
                if ((serialDataCh0Queue.Count != total_frms_last) && (paint_browse_mode == true))
                {
                    total_frms_last = serialDataCh0Queue.Count;  // 记录上一次的帧数

                    chart1.Series[0].Points.DataBindY(serialDataCh0Queue.ToArray());
                    chart1.Series[1].Points.DataBindY(serialDataCh1Queue.ToArray());
                    chart1.Series[2].Points.DataBindY(serialDataCh2Queue.ToArray());
                    chart1.Series[3].Points.DataBindY(serialDataCh3Queue.ToArray());
                    chart1.Series[4].Points.DataBindY(serialDataCh4Queue.ToArray());
                    chart1.Series[5].Points.DataBindY(serialDataCh5Queue.ToArray());

                    if (serialDataCh0Queue.Count >= 6000)
                    {
                        CreateArrayStoreQueue(page_cnt);
                        current_page = page_cnt;
                        ClearReceivedQueue();  // 清除接收队列
                        page_cnt++;
                        numericUpDown1.Maximum = page_cnt;  // 限制最大值
                        numericUpDown1.UpButton();  // 增加1次
                        if (page_cnt == page_max_num)
                        {
                            page_cnt = 0; // 覆盖Page0
                        }
                    }
                    else
                    {
                        ;
                    }

                }
            }
        }

        // 创建数组储存队列
        private void CreateArrayStoreQueue(int page)
        {
            JaggedArrayCh0[page] = serialDataCh0Queue.ToArray();
            JaggedArrayCh1[page] = serialDataCh1Queue.ToArray();
            JaggedArrayCh2[page] = serialDataCh2Queue.ToArray();
            JaggedArrayCh3[page] = serialDataCh3Queue.ToArray();
            JaggedArrayCh4[page] = serialDataCh4Queue.ToArray();
            JaggedArrayCh5[page] = serialDataCh5Queue.ToArray();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 窗体关闭时的动作
            if (paint_thread != null)
            {
                if (paint_thread.IsAlive)
                {
                    paint_thread.Abort();
                }
            }
        }

        private void 关闭ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void 打开ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {

        }

        private void radioButton1_CheckedChanged_1(object sender, EventArgs e)
        {
            if (radioButton1.Checked == true)
            {
                paint_browse_mode = true;
            }
            else
            {
                paint_browse_mode = false;
            }
        }

        private void radioButton2_CheckedChanged_1(object sender, EventArgs e)
        {
            if (radioButton2.Checked == true)
            {
                paint_browse_mode = false;
            }
            else
            {
                paint_browse_mode = true;
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            // 先判断是否处于浏览模式
            if (true == paint_browse_mode)
            {
                if (MessageBox.Show("保存数据需切换到浏览模式，确认吗?", "提示", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    radioButton2.PerformClick();  // 进入浏览模式
                    data2csvFile();        // Do something after the Yes button was clicked by user.
                }
                else
                {
                    return;  // Do something after the No button was clicked by user.
                }
            }
            else
            {
                data2csvFile();        // Do something after the Yes button was clicked by user.
            }
        }

        private void data2csvFile()
        {
            string app_path = System.IO.Directory.GetCurrentDirectory();
            var myExport = new CsvFileDeal();
            string fileName = null;

            // 以下为保存当前页面中的数据
            while (save_index < chart1.Series[0].Points.Count)
            {
                myExport.AddRow();
                myExport["Cnt"] = save_index.ToString();
                myExport["ch0"] = chart1.Series[0].Points[save_index].YValues[0].ToString();
                myExport["ch1"] = chart1.Series[1].Points[save_index].YValues[0].ToString();
                myExport["ch2"] = chart1.Series[2].Points[save_index].YValues[0].ToString();
                myExport["ch3"] = chart1.Series[3].Points[save_index].YValues[0].ToString();
                myExport["ch4"] = chart1.Series[4].Points[save_index].YValues[0].ToString();
                myExport["ch5"] = chart1.Series[5].Points[save_index].YValues[0].ToString();
                
                save_index++;
            }
            save_index = 0;

            // 保存文件对话框
            SaveFileDialog savefile = new SaveFileDialog();
            //如果文件名未写后缀名则自动添加     *.*不会自动添加后缀名
            savefile.AddExtension = true;
            savefile.InitialDirectory = app_path;  // 当前路径
            savefile.Filter = "数据文件|.csv";
            //savefile.Filter = "可执行文件|*.exe|文本文件|*.txt|STOCK|STOCK.txt|所有文件|*.*";

            if (DialogResult.OK == savefile.ShowDialog())
            {
                textBox1.AppendText("保存路径：" + savefile.FileName + "\r\n");
                fileName = savefile.FileName;
            }
            if (fileName == null)
            {
                MessageBox.Show("未输入文件名，不保存！", "Warning!");
            }
            else
            {
                myExport.ExportToFile(fileName);
                MessageBox.Show("数据已保存");
            }

            //paint_browse_mode = true;  // 保存完成后可绘图

        }

        public string GetTimeStamp()
        {
            DateTime dt = DateTime.Now;

            return dt.ToString();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            // 提示用户数据覆盖情况

            if (MessageBox.Show("加载数据会覆盖当前界面，确认吗?", "提示", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                radioButton2.PerformClick();  // 进入浏览模式

            }
            else
            {
                return;  // Do something after the No button was clicked by user.
            }

            OpenFileDialog openfile = new OpenFileDialog();

            //初始显示文件目录
            //openfile.InitialDirectory = @"";
            openfile.Title = "请选择要打开的文件";
            //过滤文件类型
            openfile.Filter = "数据文件|*.csv";
            if (DialogResult.OK == openfile.ShowDialog())
            {
                //将选择的文件的全路径赋值给文本框
                //textBox1.Text = openfile.FileName;
                button7.PerformClick();  // 清除界面再加载
                total_frms_last = 0;  // 确保每次都重新绘图
            }
            else
            {
                return;
            }

            int line = 0;
            using (var streamRdr = new StreamReader(openfile.FileName))
            {
                var csv = new CsvReader(streamRdr, ",");
                //textBox1.AppendText("Read begin.\t");
                while (csv.Read())
                {
                    // 按照列进行读取
                    line++;
                    if (line <= 2)
                    {
                        continue; // skip header row
                    }
                    //textBox1.AppendText(csv[0] + "\t");  // 占用太多时间

                    serialDataCh0Queue.Enqueue(Convert.ToInt32(csv[1]));
                    serialDataCh1Queue.Enqueue(Convert.ToInt32(csv[2]));
                    serialDataCh2Queue.Enqueue(Convert.ToInt32(csv[3]));
                    serialDataCh3Queue.Enqueue(Convert.ToInt32(csv[4]));
                    serialDataCh4Queue.Enqueue(Convert.ToInt32(csv[5]));
                    serialDataCh5Queue.Enqueue(Convert.ToInt32(csv[6]));
                }
                //textBox1.AppendText("Read end.\t");

                // 开启绘图线程
                //if (paint_thread.IsAlive != true)
                //{
                //    paint_thread.Start(); //启动线程
                //}
            }
            SerialDataQueueBind2Chart();  // 绘图一次
            radioButton2.PerformClick();  // 进入浏览模式
        }

        // 计算 X轴刻度线
        private void CalcBestAxisInterval(bool mode)
        {
            int count = 0;

            if (mode == true)
            {
                count = (int)(chart1.ChartAreas[0].AxisX.ScaleView.ViewMaximum - chart1.ChartAreas[0].AxisX.ScaleView.ViewMinimum) / 20;
                chart1.ChartAreas[0].AxisX.Interval = count;
            }
            else
            {
                chart1.ChartAreas[0].AxisX.Interval = 0;  // 设置为自动
            }

        }

        private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            // 重置1步
            if (e.ClickedItem == toolStripMenuItem1)
            {
                CalcBestAxisInterval(false);
                checkBox9.Checked = false;
                setAxisMarkerStyle(false);
                chart1.ChartAreas[0].AxisX.ScaleView.ZoomReset(1);//ZoomReset(0)表示撤销所有放大动作
                chart1.ChartAreas[0].AxisY.ScaleView.ZoomReset(1);//ZoomReset(1)表示撤销上一次放大动作
                chart1.ChartAreas[0].AxisY2.ScaleView.ZoomReset(1);
            }
            else if (e.ClickedItem == toolStripMenuItem2)  // 重置所有
            {
                CalcBestAxisInterval(false);
                checkBox9.Checked = false;  // 先取消值的显示
                setAxisMarkerStyle(false);
                chart1.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);//ZoomReset(0)表示撤销所有放大动作
                chart1.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);//ZoomReset(1)表示撤销上一次放大动作
                chart1.ChartAreas[0].AxisY2.ScaleView.ZoomReset(0);

            }
            else if (e.ClickedItem == toolStripMenuItem3)  // 更新刻度
            {
                CalcBestAxisInterval(true);
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            int tmp_value = 0;

            tmp_value = (int)numericUpDown1.Value;
            numericUpDown1.Maximum = page_cnt;  // 限制最大值

            current_page = tmp_value;

            // 处于绘图模式下，直接返回
            if (paint_browse_mode == true)
            {
                return;
            }
            // 确保有数据才能绑定
            if (page_cnt > tmp_value)
            {
                historyArrayBind2Chart(tmp_value);
            }
            else
            {
                chart1.Series[0].Points.DataBindY(serialDataCh0Queue.ToArray());
                chart1.Series[1].Points.DataBindY(serialDataCh1Queue.ToArray());
                chart1.Series[2].Points.DataBindY(serialDataCh2Queue.ToArray());
                chart1.Series[3].Points.DataBindY(serialDataCh3Queue.ToArray());
                chart1.Series[4].Points.DataBindY(serialDataCh4Queue.ToArray());
                chart1.Series[5].Points.DataBindY(serialDataCh5Queue.ToArray());
            }

            //textBox1.AppendText(numericUpDown1.Value.ToString());  // 调试
        }

        // 将数据绑定到chart中
        private void historyArrayBind2Chart(int page)
        {
            if (paint_browse_mode == false)
            {
                chart1.Series[0].Points.DataBindY(JaggedArrayCh0[page]);
                chart1.Series[1].Points.DataBindY(JaggedArrayCh1[page]);
                chart1.Series[2].Points.DataBindY(JaggedArrayCh2[page]);
                chart1.Series[3].Points.DataBindY(JaggedArrayCh3[page]);
                chart1.Series[4].Points.DataBindY(JaggedArrayCh4[page]);
                chart1.Series[5].Points.DataBindY(JaggedArrayCh5[page]);
            }

        }

        private void ClearReceivedQueue()
        {
            // 清除队列值
            serialDataCh0Queue.Clear();
            serialDataCh1Queue.Clear();
            serialDataCh2Queue.Clear();
            serialDataCh3Queue.Clear();
            serialDataCh4Queue.Clear();
            serialDataCh5Queue.Clear();
        }

        private void SerialDataQueueBind2Chart()
        {
            chart1.Series[0].Points.DataBindY(serialDataCh0Queue.ToArray());
            chart1.Series[1].Points.DataBindY(serialDataCh1Queue.ToArray());
            chart1.Series[2].Points.DataBindY(serialDataCh2Queue.ToArray());
            chart1.Series[3].Points.DataBindY(serialDataCh3Queue.ToArray());
            chart1.Series[4].Points.DataBindY(serialDataCh4Queue.ToArray());
            chart1.Series[5].Points.DataBindY(serialDataCh5Queue.ToArray());
        }

        private void 暂停ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("断点，程序暂停。");
        }

        private void chart1_MouseMove(object sender, MouseEventArgs e)
        {

        }

        // 显示x-y轴对应信息
        private void chart1_MouseClick(object sender, MouseEventArgs e)
        {
            label3.Visible = true;
            label3.Location = new Point(950, 100);

            double xValue = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.X);

            int x = (int)xValue;

            if ((x < chart1.Series[4].Points.Count) && (x >= 0))
            {
                label3.Text = "x: " + x.ToString() + "\r\n" +
                chart1.Series[0].LegendText + ": " + chart1.Series[0].Points[x].YValues[0].ToString() + "   " +
                chart1.Series[1].LegendText + ": " + chart1.Series[1].Points[x].YValues[0].ToString() + "   " +
                chart1.Series[2].LegendText + ": " + chart1.Series[2].Points[x].YValues[0].ToString() + "\r\n" +
                chart1.Series[3].LegendText + ": " + chart1.Series[3].Points[x].YValues[0].ToString() + "   " +
                chart1.Series[4].LegendText + ": " + chart1.Series[4].Points[x].YValues[0].ToString() + "   " +
                chart1.Series[5].LegendText + ": " + chart1.Series[5].Points[x].YValues[0].ToString() + "\r\n";
            }
        }
    }
}
