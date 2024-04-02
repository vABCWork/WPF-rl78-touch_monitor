using ScottPlot;
using ScottPlot.MarkerShapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TouchMonitor
{

    // タッチセンサのデータ用クラスの定義
    public class CTSUData
    {
        public string ts_name { get; set; }      // TS端子名　(TS06,TS08,TS10,TS13)

        public UInt16 ctsusc { get; set; }   // TSセンサカウント値(0～65535)

        public UInt16 ctsurc { get; set; }   // リファレンスカウント値 (0～65535)

        public UInt16 ctsussc { get; set; }   // CTSU 高域ノイズ低減スペクトラム拡散制御レジスタ（CTSUSSC）

        public UInt16 ctsuso0 { get; set; }   // CTSU センサオフセットレジスタ0（CTSUSO0）

        public UInt16 ctsuso1 { get; set; }    // CTSU センサオフセットレジスタ1（CTSUSO1）
    }


    // 履歴(ヒストリ)データ　クラス
    // クラス名: HistoryData
    // メンバー:  double  data0
    //            double  data1
    //            double  data2
    //            double  data3
    //            double  th
    //            double  dt
    //

    public class HistoryData
    {
        public double data0 { get; set; }       //  TS06
        public double data1 { get; set; }       //  TS08
        public double data2 { get; set; }       //  TS10
        public double data3 { get; set; }       //  TS13

        public double th { get; set; }          //  ON/OFF判定用の値(Threshold) 
        public double dt { get; set; }         // 日時 (double型)
    }


    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public static byte[] sendBuf;          // 送信バッファ   
        public static int sendByteLen;         //　送信データのバイト数

        public static byte[] rcvBuf;           // 受信バッファ
        public static int srcv_pt;             // 受信データ格納位置

        public static DateTime receiveDateTime;           // 受信完了日時

        public static DispatcherTimer SendIntervalTimer;  // タイマ　モニタ用　電文送信間隔   
        DispatcherTimer RcvWaitTimer;                    // タイマ　受信待ち用 



        public static ushort send_msg_cnt;              // 送信数 
        public static ushort disp_msg_cnt_max;          // 送受信文の表示最大数

        public static int commlog_window_cnt;           // 通信ログ用ウィンドウの表示個数


        public static ObservableCollection<CTSUData> ctsu_list;       // タッチセンサのデータ
                                                                      
        public static string[] touch_bt_name;           // タッチセンサの名称

        uint trend_data_item_max;             // 各リアルタイム　トレンドデータの保持数 

        double[] trend_data0;                 // トレンドデータ 0  TS06  CTSU センサカウンタ CTSUSC  
        double[] trend_data1;                 // トレンドデータ 1  TS08          :                
        double[] trend_data2;                 // トレンドデータ 2  TS10          :
        double[] trend_data3;                 // トレンドデータ 3  TS13          :

        double[] trend_th;                     // トレンドデータ  ON/OFF判定用の値(Threshold) 

        double[] trend_dt;                    // トレンドデータ　収集日時


        ScottPlot.Plottable.ScatterPlot trend_scatter_0; // トレンドデータ0  TS06
        ScottPlot.Plottable.ScatterPlot trend_scatter_1; // トレンドデータ1  TS08
        ScottPlot.Plottable.ScatterPlot trend_scatter_2; // トレンドデータ2  TS10
        ScottPlot.Plottable.ScatterPlot trend_scatter_3; // トレンドデータ3  TS13

        ScottPlot.Plottable.ScatterPlot trend_scatter_th; // トレンドデータ   ON/OFF判定用の値(Threshold) 

        public List<HistoryData> historyData_list;          // ヒストリデータ　データ収集時に使用

        double threshold_value;             // ON/OFF判定用の値

        UInt16 ch1_cnt;                 // ch1 現在値
        UInt16 ch1_min;                 // ch1 最小値
        UInt16 ch1_max;                 // ch1 最大値

        UInt16 ch2_cnt;                 // ch2 現在値
        UInt16 ch2_min;                 // ch2 最小値
        UInt16 ch2_max;                 // ch2 最大値

        UInt16 ch3_cnt;                 // ch3 現在値
        UInt16 ch3_min;                 // ch3 最小値
        UInt16 ch3_max;                 // ch3 最大値

        UInt16 ch4_cnt;                 // ch4 現在値
        UInt16 ch4_min;                 // ch4 最小値
        UInt16 ch4_max;                 // ch4 最大値

        Boolean read_1st_flag = true;   // 最初の読み出し = true, 2回目移行 = false


        public static uint rcvmsg_proc_cnt;  // RcvMsgProc()の実行回数    (デバック用)
        public static byte rcvmsg_proc_flg;  // RcvMsgProc()の実行中 = 1 (デバック用)                           

        public MainWindow()
        {
            InitializeComponent();


            ConfSerial.serialPort = new SerialPort();    // シリアルポートのインスタンス生成

             ConfSerial.serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);  // データ受信時のイベント処理

            sendBuf = new byte[2048];     // 送信バッファ領域  serialPortのWriteBufferSize =2048 byte(デフォルト)
            rcvBuf = new byte[4096];      // 受信バッファ領域   SerialPort.ReadBufferSize = 4096 byte (デフォルト)

            disp_msg_cnt_max = 1000;        // 送受信文の表示最大数

            SendIntervalTimer = new System.Windows.Threading.DispatcherTimer();　　// タイマーの生成(定周期モニタ用)
            SendIntervalTimer.Tick += new EventHandler(SendIntervalTimer_Tick);  // タイマーイベント
            SendIntervalTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);         // タイマーイベント発生間隔 1sec(コマンド送信周期)

            RcvWaitTimer = new System.Windows.Threading.DispatcherTimer();　 // タイマーの生成(受信待ちタイマ)
            RcvWaitTimer.Tick += new EventHandler(RcvWaitTimer_Tick);        // タイマーイベント
            RcvWaitTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);          // タイマーイベント発生間隔 (受信待ち時間)


            ctsu_list = new ObservableCollection<CTSUData>();   // データバインディングするため、ObservableCollectionで生成
            this.CTSU_DataGrid.ItemsSource = ctsu_list;         // データグリッド(CTSU_DataGrid)のデータソース指定

            touch_bt_name = new string[] {"TS06(SW4)","TS08(SW3)","TS10(SW2)","TS13(SW1)" }; // タッチセンサの名称
            threshold_value = 15000;             // ON/OFF判定用の値

            historyData_list = new List<HistoryData>();     // モニタ時のトレンドデータ 記録用　

            Chart_Ini();                        // チャート(リアルタイム用)の初期化

        }

        //
        // 　チャートの初期化(リアルタイム　チャート用)
        //
        private void Chart_Ini()
        {
            trend_data_item_max = 30;             // 各リアルタイム　トレンドデータの保持数(=30 ) 1秒毎に収集すると、30秒分のデータ

            trend_data0 = new double[trend_data_item_max];
            trend_data1 = new double[trend_data_item_max];
            trend_data2 = new double[trend_data_item_max];
            trend_data3 = new double[trend_data_item_max];
            trend_th = new double[trend_data_item_max];

            trend_dt = new double[trend_data_item_max];

            DateTime datetime = DateTime.Now;   // 現在の日時

            DateTime[] myDates = new DateTime[trend_data_item_max];


            
            for (int i = 0; i < trend_data_item_max; i++)  // 初期値の設定
            {
                trend_data0[i] = 13000 + i;
                trend_data1[i] = 12000 + i;
                trend_data2[i] = 11000 + i;
                trend_data3[i] = 10000 + i;

                trend_th[i] = threshold_value;

                myDates[i] = datetime + new TimeSpan(0, 0, i);  // i秒増やす

                trend_dt[i] = myDates[i].ToOADate();   // (現在の日時 + i 秒)をdouble型に変換
            }

            // X軸の日時リミットを、最終日時+1秒にする
            DateTime dt_end = DateTime.FromOADate(trend_dt[trend_data_item_max - 1]); // double型を　DateTime型に変換
            TimeSpan dt_sec = new TimeSpan(0, 0, 1);    // 1 秒
            DateTime dt_limit = dt_end + dt_sec;      // DateTime型(最終日時+ 1秒) 
            double dt_ax_limt = dt_limit.ToOADate();   // double型(最終日時+ 1秒) 

            wpfPlot_Trend.Refresh();        // データ変更後のリフレッシュ
           
            trend_scatter_0 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data0, color: System.Drawing.Color.Blue, label: "TS06(SW4)"); // プロット plot the data array only once
            trend_scatter_1 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data1, color: System.Drawing.Color.Gainsboro, label: "TS08(SW3)");
            trend_scatter_2 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data2, color: System.Drawing.Color.Orange, label: "TS10(SW2)");
            trend_scatter_3 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data3, color: System.Drawing.Color.Green, label: "TS13(SW1)"); 

            trend_scatter_th = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_th, color: System.Drawing.Color.Red, lineWidth:3, markerSize:0, markerShape:MarkerShape.none, lineStyle: LineStyle.Solid, label: "Threshold");
 
            wpfPlot_Trend.Configuration.Pan = false;               // パン(グラフの移動)不可
            wpfPlot_Trend.Configuration.ScrollWheelZoom = true;   // ズーム(グラフの拡大、縮小)可

            wpfPlot_Trend.Plot.SetAxisLimits(trend_dt[0], dt_ax_limt, 0, 65535);  // X軸の最小=現在の時間 ,X軸の最大=最終日時+1秒,Y軸最小=0, Y軸最大=65535

            wpfPlot_Trend.Plot.XAxis.Ticks(true, false, true);         // X軸の大きい目盛り=表示, X軸の小さい目盛り=非表示, X軸の目盛りのラベル=表示
            wpfPlot_Trend.Plot.XAxis.TickLabelStyle(fontSize: 16);

            wpfPlot_Trend.Plot.XAxis.TickLabelFormat("HH:mm:ss", dateTimeFormat: true); // X軸　時間の書式(例 12:30:15)、X軸の値は、日時型

            wpfPlot_Trend.Plot.XAxis.Label(label: "time", color: System.Drawing.Color.Black);  // X軸全体のラベル
            wpfPlot_Trend.Plot.YAxis.TickLabelStyle(fontSize: 16);     // Y軸   ラベルのフォントサイズ変更  :

            wpfPlot_Trend.Plot.YAxis.Label(label: "count", color: System.Drawing.Color.Black);    // Y軸全体のラベル


            var legend1 = wpfPlot_Trend.Plot.Legend(enable: true, location: Alignment.UpperLeft);   // 凡例の表示
     
            legend1.FontSize = 12;      // 凡例のフォントサイズ
        
        }



        // 定周期モニタ用 
        //  rd_mlx_em_flg = 0 の場合、 MLX90640のRAM読み出しコマンドのセット
        //                = 1 の場合、 放射率、熱電対の温度読み出しコマンドのセット
        private void SendIntervalTimer_Tick(object sender, EventArgs e)
        {
            ctsu_read_cmd_set();        //  CTSUデータ 読み出し コマンド(0x40)のセット

            bool fok = send_disp_data();       // データ送信
        }


        // 送信後、1000msec以内に受信文が得られないと、受信エラー
        //  
        private void RcvWaitTimer_Tick(object sender, EventArgs e)
        {

            RcvWaitTimer.Stop();        // 受信監視タイマの停止

            StatusTextBlock.Text = "Receive time out";
        }

        private delegate void DelegateFn();

        // データ受信時のイベント処理
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if (rcvmsg_proc_flg == 1)     //  RcvMsgProc()の実行中の場合、処理しない
            {
                return;
            }

            int id = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Console.WriteLine("DataReceivedHandlerのスレッドID : " + id);

            int rd_num = ConfSerial.serialPort.BytesToRead;       // 受信データ数

            ConfSerial.serialPort.Read(rcvBuf, srcv_pt, rd_num);   // 受信データの読み出し

            srcv_pt = srcv_pt + rd_num;     // 次回の保存位置

            int rcv_total_byte = 0;

            if (rcvBuf[0] == 0xc0)             // ctsu 読み出しコマンド(0x40)のレスポンス(0xc0)の場合
            {
                rcv_total_byte = 46;
            }


            if (srcv_pt == rcv_total_byte)  // 最終データ受信済み 
            {
                RcvWaitTimer.Stop();        // 受信監視タイマー　停止

                receiveDateTime = DateTime.Now;   // 受信完了時刻を得る

                rcvmsg_proc_flg = 1;       // RcvMsgProc()の実行中

                Dispatcher.BeginInvoke(new DelegateFn(RcvMsgProc)); // Delegateを生成して、RcvMsgProcを開始   (表示は別スレッドのため)
            }

        }


        //  
        //  最終データ受信後の処理
        //   表示
        //  
        private void RcvMsgProc()
        {
            if (rcvBuf[0] == 0xc0)     // ctsu 読み出しコマンド(0x40)のレスポンス(0xc0)の場合
            {
                Disp_ctsu_data();   //  ctsuデータの表示

                read_1st_flag = false;   // 2回目以降の読み出し
            }

            if (CommLog.rcvframe_list != null)
            {
                CommLog.rcvmsg_disp();          // 受信データの表示       
            }

            rcvmsg_proc_cnt++;         // RcvMsgProc()の実行回数　インクリメント

            rcvmsg_proc_flg = 0;       // RcvMsgProc()の完了

        }


        // 
        // CTSUデータ 読み出し コマンド(0x40)のセット
        // 
        private void ctsu_read_cmd_set()
        {
            UInt16 crc_cd;

            sendBuf[0] = 0x40;           // 送信コマンド  0x40 　
            sendBuf[1] = 0;              //　
            sendBuf[2] = 0;
            sendBuf[3] = 0;
            sendBuf[4] = 0;
            sendBuf[5] = 0;

            crc_cd = CRC_sendBuf_Cal(6);     // CRC計算

            sendBuf[6] = (Byte)(crc_cd >> 8); // CRCは上位バイト、下位バイトの順に送信
            sendBuf[7] = (Byte)(crc_cd & 0x00ff);

            sendByteLen = 8;                   // 送信バイト数


        }


        // マイコンから読み出した　ctsu データを表示
        // 受信データ:
        // rcvBuf[ ]:
        //        0 :コマンドに対するレスポンス(0xc0)
        //        1 :CTSU ステータスレジスタ（CTSUST）
        //        2 :CTSU エラーステータスレジスタ（CTSUERRS）(下位バイト側)
        //        3 :	　　　　　　　　　　:　　　　　　　　 (上位バイト側)
        //        4 :TS06 センサカウンタ（CTSUSC）(下位バイト側)
        //        5 :	                   ：　　(上位バイト側)
        //        6 :TS06 リファレンスカウンタ（CTSURC)  (下位バイト側)      
        //        7 :	                   ：　　　　　　(上位バイト側)
        //        8 :TS06 高域ノイズ低減スペクトラム拡散制御レジスタ（CTSUSSC）(下位バイト側)      
        //        9 :	                   ：　　　　　　　　　　(上位バイト側)
        //        10:TS06 センサオフセットレジスタ0（CTSUSO0)  (下位バイト側) 
        //        11: 	                   ：　　　　　　　　　(上位バイト側)
        //        12:TS06 センサオフセットレジスタ1（CTSUSO1)  (下位バイト側) 
        //        13: 	                   ：　　　　　　　　　(上位バイト側)
        //        14:TS08 センサカウンタ（CTSUSC）(下位バイト側)
        //        15: 	                   ：　　 (上位バイト側)
        //        16:TS08 リファレンスカウンタ（CTSURC)  (下位バイト側)      
        //        17: 	                   ：　　　　　　(上位バイト側)
        //        18:TS08 高域ノイズ低減スペクトラム拡散制御レジスタ（CTSUSSC）(下位バイト側)      
        //        19: 	                   ：　　　　　　(上位バイト側)
        //        20:TS08 センサオフセットレジスタ0（CTSUSO0)  (下位バイト側) 
        //        21: 	                 ：　　　　　　　　　　(上位バイト側)
        //        22:TS08 センサオフセットレジスタ1（CTSUSO1)  (下位バイト側) 
        //        23: 	                 ：　　　　　　　　　　(上位バイト側)
        //        24:TS10 センサカウンタ（CTSUSC）(下位バイト側)
        //        25: 	    ：　　　　　　　　　　(上位バイト側)
        //        26:TS10 リファレンスカウンタ（CTSURC)  (下位バイト側)      
        //        27: 	           ：　　　　　　　　　　(上位バイト側)
        //        28:TS10 高域ノイズ低減スペクトラム拡散制御レジスタ（CTSUSSC）(下位バイト側)      
        //        29: 	                   ：　　　　　　(上位バイト側)
        //        30:TS10 センサオフセットレジスタ0（CTSUSO0)  (下位バイト側) 
        //        31: 	                 ：　　　　　　　　　　(上位バイト側)
        //        32:TS10 センサオフセットレジスタ1（CTSUSO1)  (下位バイト側) 
        //        33: 	                 ：　　　　　　　　　　(上位バイト側)
        //        34:TS13 センサカウンタ（CTSUSC）(下位バイト側)
        //        35: 	    ：　　　　　　　　　　(上位バイト側)
        //        36:TS13 リファレンスカウンタ（CTSURC)  (下位バイト側)      
        //        37: 	           ：　　　　　　　　　　(上位バイト側)
        //        38:TS13 高域ノイズ低減スペクトラム拡散制御レジスタ（CTSUSSC）(下位バイト側)      
        //        39: 	           ：　　　　　　　　　　(上位バイト側)
        //        40:TS13 センサオフセットレジスタ0（CTSUSO0)  (下位バイト側) 
        //        41: 	                 ：　　　　　　　　　　(上位バイト側)
        //        42:TS13 センサオフセットレジスタ1（CTSUSO1)  (下位バイト側) 
        //        43: 	       
        //        44: 	CRC 上位バイト
        //        45: 	CRC 下位バイト
        //

        private void Disp_ctsu_data()
        {
            UInt16 crc_cd;
            UInt16 pt;

            crc_cd = CRC_rcvBuf_Cal(46);         // 全データのCRC計算             

            if (crc_cd != 0)
            {
                AlarmTextBlock.Text = "Receive CRC Err.";
                SendIntervalTimer.Stop();     // データ収集用コマンド送信タイマー停止
                return;
            }
            else
            {
                AlarmTextBlock.Text = "";
            }

            ctsu_list.Clear();        // クリア

      

                                                // 受信データより、4ch(TS06,TS08,TS10,TS13)分の
            for (int i = 0; i < 4; i = i + 1)   // CTSUSC,CTSURC,CTSUSSC,CTSUSO0,CTSUSO1を 読み出し
            {
                CTSUData ctsudata = new CTSUData();

                ctsudata.ts_name = touch_bt_name[i];   //タッチセンサの名称

                pt = (UInt16)(i * 10 + 4);    // 各チャンネルのセンサカウント値(CTSUSC)の位置
               
                ctsudata.ctsusc = (ushort)(( rcvBuf[pt] )  |   (rcvBuf[pt + 1] << 8)); // CTSUSC
                ctsudata.ctsurc = (ushort)(( rcvBuf[pt + 2]) | (rcvBuf[pt + 3] << 8)); // CTSURC
                ctsudata.ctsussc = (ushort)((rcvBuf[pt + 4]) | (rcvBuf[pt + 5] << 8)); // CTSUSSC
                ctsudata.ctsuso0 = (ushort)((rcvBuf[pt + 6]) | (rcvBuf[pt + 7] << 8)); // CTSUSSO0
                ctsudata.ctsuso1 = (ushort)((rcvBuf[pt + 8]) | (rcvBuf[pt + 9] << 8)); // CTSUSSO1

                ctsu_list.Add(ctsudata);   // データの追加 (データバインディングにより、データグリッド(CTSU_DataGrid)へ表示)
            }

            UInt16 ctsuerrs = BitConverter.ToUInt16(rcvBuf, 2); // rcvBuf[2]から uint16へ
            CTSUERRS_TextBlock.Text = "0x" + ctsuerrs.ToString("x4");
            if ((ctsuerrs & 0x8000) == 0x8000) //CTSUICOMP = 1 :TSCAP 電圧異常
            {
                CTSUERRS_Info_TextBlock.Text = "TSCAP 電圧異常";  // CTSUSO0 レジスタで設定したオフセット電流量が、
                                                                  // タッチ計測時のセンサICO 入力電流を上回った場合、
                                                                  // TSCAP 電圧が異常となり、タッチ計測が正しく行われません。
            }
            else
            {
                CTSUERRS_Info_TextBlock.Text = "";
            }

            ch1_cnt = BitConverter.ToUInt16(rcvBuf, 4);    // rcvBuf[4]から uint16へ
            Ch1_TextBox.Text = ch1_cnt.ToString();         // TS06 センサカウンタ値（CTSUSC）

            ch2_cnt = BitConverter.ToUInt16(rcvBuf, 14);    // rcvBuf[14]から uint16へ
            Ch2_TextBox.Text = ch2_cnt.ToString();         // TS08 センサカウンタ値（CTSUSC）
         
            ch3_cnt = BitConverter.ToUInt16(rcvBuf, 24);    // rcvBuf[24]から uint16へ
            Ch3_TextBox.Text = ch3_cnt.ToString();         // TS10 センサカウンタ値（CTSUSC）

            ch4_cnt = BitConverter.ToUInt16(rcvBuf, 34);    // rcvBuf[34]から uint16へ
            Ch4_TextBox.Text = ch4_cnt.ToString();         // TS13 センサカウンタ値（CTSUSC）

            if (read_1st_flag == true) // 最初の読み出しの場合
            {
                ch1_min = ch1_cnt;
                ch1_max = ch1_cnt;

                ch2_min = ch2_cnt;
                ch2_max = ch2_cnt;

                ch3_min = ch3_cnt;
                ch3_max = ch3_cnt;

                ch4_min = ch4_cnt;
                ch4_max = ch4_cnt;
            }
            else                     // 2回目以降の読み出しの場合
            {
                disp_ch_min_max();   // 各チャンネルの最大、最小 表示
            }


            Threshold_TextBox_ReadOnly.Text = threshold_value.ToString();   // // ON/OFF判定用の値 (threshold)

            // 1スキャン前のデータを移動後、最新のデータを入れる
            Array.Copy(trend_data0, 1, trend_data0, 0, trend_data_item_max - 1);
            trend_data0[trend_data_item_max - 1] =　ch1_cnt;

            Array.Copy(trend_data1, 1, trend_data1, 0, trend_data_item_max - 1);
            trend_data1[trend_data_item_max - 1] = ch2_cnt;

            Array.Copy(trend_data2, 1, trend_data2, 0, trend_data_item_max - 1);
            trend_data2[trend_data_item_max - 1] = ch3_cnt;

            Array.Copy(trend_data3, 1, trend_data3, 0, trend_data_item_max - 1);
            trend_data3[trend_data_item_max - 1] = ch4_cnt;

           
            Array.Copy(trend_th, 1, trend_th, 0, trend_data_item_max - 1);
            trend_th[trend_data_item_max - 1] = threshold_value;

            Array.Copy(trend_dt, 1, trend_dt, 0, trend_data_item_max - 1);
            trend_dt[trend_data_item_max - 1] = receiveDateTime.ToOADate();    // 受信日時 double型に変換して、格納



            wpfPlot_Trend.Render();   // リアルタイム グラフの更新

            wpfPlot_Trend.Plot.AxisAuto();     // X軸の範囲を更新


        }


        // 各チャンネルの最大、最小 表示
        private void disp_ch_min_max()
        {
            if (read_1st_flag == true) // 最初の読み出しの場合
            {
                ch1_min = ch1_cnt;
                ch1_max = ch1_cnt;

                ch2_min = ch2_cnt;
                ch2_max = ch2_cnt;

                ch3_min = ch3_cnt;
                ch3_max = ch3_cnt;

                ch4_min = ch4_cnt;
                ch4_max = ch4_cnt;
            }
            else                     // 2回目以降の読み出しの場合
            {
                if (ch1_cnt < ch1_min)
                {
                    ch1_min = ch1_cnt;
                }
                else if (ch1_cnt > ch1_max)
                {
                    ch1_max = ch1_cnt;
                }

                if (ch2_cnt < ch2_min)
                {
                    ch2_min = ch2_cnt;
                }
                else if (ch2_cnt > ch2_max)
                {
                    ch2_max = ch2_cnt;
                }

                if (ch3_cnt < ch3_min)
                {
                    ch3_min = ch3_cnt;
                }
                else if (ch3_cnt > ch3_max)
                {
                    ch3_max = ch3_cnt;
                }

                if (ch4_cnt < ch4_min)
                {
                    ch4_min = ch4_cnt;
                }
                else if (ch4_cnt > ch4_max)
                {
                    ch4_max = ch4_cnt;
                }
            }


            Ch1_Min_TextBox.Text = ch1_min.ToString();
            Ch1_Max_TextBox.Text = ch1_max.ToString();

            Ch2_Min_TextBox.Text = ch2_min.ToString();
            Ch2_Max_TextBox.Text = ch2_max.ToString();

            Ch3_Min_TextBox.Text = ch3_min.ToString();
            Ch3_Max_TextBox.Text = ch3_max.ToString();

            Ch4_Min_TextBox.Text = ch4_min.ToString();
            Ch4_Max_TextBox.Text = ch4_max.ToString();

        }


        // CRCの計算 (受信バッファ用)
        // rcvBuf[]内のデータのCRCコードを作成
        //
        // 入力 size:データ数
        // 
        //  CRC-16 CCITT:
        //  多項式: X^16 + X^12 + X^5 + 1　
        //  初期値: 0xffff
        //  MSBファースト
        //  非反転出力
        // 
        private UInt16 CRC_rcvBuf_Cal(UInt16 size)
        {
            UInt16 crc;

            UInt16 i;

            crc = 0xffff;

            for (i = 0; i < size; i++)
            {
                crc = (UInt16)((crc >> 8) | ((UInt16)((UInt32)crc << 8)));

                crc = (UInt16)(crc ^ rcvBuf[i]);
                crc = (UInt16)(crc ^ (UInt16)((crc & 0xff) >> 4));
                crc = (UInt16)(crc ^ (UInt16)((crc << 8) << 4));
                crc = (UInt16)(crc ^ (((crc & 0xff) << 4) << 1));
            }

            return crc;
        }

        // CRCの計算 (送信バッファ用)
        // sendBuf[]内のデータのCRCコードを作成
        //
        // 入力 size:データ数
        // 
        //  CRC-16 CCITT:
        //  多項式: X^16 + X^12 + X^5 + 1　
        //  初期値: 0xffff
        //  MSBファースト
        //  非反転出力
        // 
        public static UInt16 CRC_sendBuf_Cal(UInt16 size)
        {
            UInt16 crc;

            UInt16 i;

            crc = 0xffff;

            for (i = 0; i < size; i++)
            {
                crc = (UInt16)((crc >> 8) | ((UInt16)((UInt32)crc << 8)));

                crc = (UInt16)(crc ^ sendBuf[i]);
                crc = (UInt16)(crc ^ (UInt16)((crc & 0xff) >> 4));
                crc = (UInt16)(crc ^ (UInt16)((crc << 8) << 4));
                crc = (UInt16)(crc ^ (((crc & 0xff) << 4) << 1));
            }

            return crc;

        }


        //  送信と送信データの表示
        // sendBuf[]のデータを、sendByteLenバイト　送信する
        // 戻り値  送信成功時: true
        //         送信失敗時: false

        public bool send_disp_data()
        {
            if (ConfSerial.serialPort.IsOpen == true)
            {
                srcv_pt = 0;                   // 受信データ格納位置クリア

                ConfSerial.serialPort.Write(sendBuf, 0, sendByteLen);     // データ送信

                if (CommLog.sendframe_list != null)
                {
                    CommLog.sendmsg_disp();          // 送信データの表示
                }

                send_msg_cnt++;              // 送信数インクリメント 

                RcvWaitTimer.Start();        // 受信監視タイマー　開始

                StatusTextBlock.Text = "";
                return true;
            }

            else
            {
                StatusTextBlock.Text = "Comm port closed !";
                SendIntervalTimer.Stop();
                return false;
            }
        }



        // Start ボタンを押した時の処理
        private void Start_Monitor_Button_Click(object sender, RoutedEventArgs e)
        {
            send_msg_cnt = 0;            // 送信数のクリア
            rcvmsg_proc_cnt = 0;         // RcvMsgProc()の実行回数 (デバック用)

            SendIntervalTimer.Start();   // 定周期　送信用タイマの開始
        }

        // Stop ボタンを押した時の処理
        private void Stop_Monitor_Button_Click(object sender, RoutedEventArgs e)
        {
            SendIntervalTimer.Stop();     // データ収集用コマンド送信タイマー停止
        }

        //　通信ポートの設定 ダイアログを開く
        private void Serial_Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConfSerial();
            window.Owner = this;
            window.ShowDialog();
        }

        // 通信メッセージ表示用のウィンドウを開く
        private void Comm_Log_Button_Click(object sender, RoutedEventArgs e)
        {
            if (commlog_window_cnt > 0) return;   // 既に開いている場合、リターン

            var window = new CommLog();

            window.Owner = this;   // Paraウィンドウの親は、このMainWindow

            window.Show();

            commlog_window_cnt++;     // カウンタインクリメント
        }


        // コマンド１回送信 (デバック用) 
        private void One_Button_Click(object sender, RoutedEventArgs e)
        {
            ctsu_read_cmd_set();        //  CTSUデータ 読み出し コマンド(0x40)のセット

            bool fok = send_disp_data();       // データ送信
        }


        // チェックボックスによるトレンド線の表示 
        private void CH_N_Show(object sender, RoutedEventArgs e)
        {

            if (trend_scatter_0 is null) return;
            if (trend_scatter_1 is null) return;
            if (trend_scatter_2 is null) return;
            if (trend_scatter_3 is null) return;
            if (trend_scatter_th is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "Ch1_CheckBox")
            {
                trend_scatter_0.IsVisible = true;
            }
            else if (checkBox.Name == "Ch2_CheckBox")
            {
                trend_scatter_1.IsVisible = true;
            }
            else if (checkBox.Name == "Ch3_CheckBox")
            {
                trend_scatter_2.IsVisible = true;
            }
            else if (checkBox.Name == "Ch4_CheckBox")
            {
                trend_scatter_3.IsVisible = true;
            }

            else if (checkBox.Name == "Threshold_CheckBox")
            {
                trend_scatter_th.IsVisible = true;
            }

            wpfPlot_Trend.Render();   // グラフの更新

        }

        // チェックボックスによるトレンド線の非表示
        private void CH_N_Hide(object sender, RoutedEventArgs e)
        {
            if (trend_scatter_0 is null) return;
            if (trend_scatter_1 is null) return;
            if (trend_scatter_2 is null) return;
            if (trend_scatter_3 is null) return;
            if (trend_scatter_th is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "Ch1_CheckBox")
            {
                trend_scatter_0.IsVisible = false;
            }
            else if (checkBox.Name == "Ch2_CheckBox")
            {
                trend_scatter_1.IsVisible = false;
            }
            else if (checkBox.Name == "Ch3_CheckBox")
            {
                trend_scatter_2.IsVisible = false;
            }
            else if (checkBox.Name == "Ch4_CheckBox")
            {
                trend_scatter_3.IsVisible = false;
            }

            else if (checkBox.Name == "Threshold_CheckBox")
            {
                trend_scatter_th.IsVisible = false;
            }


            wpfPlot_Trend.Render();   // グラフの更新
        }

        // Thresholdの　設定ボタンが押された場合の処理
        private void Set_threhold_Button_Click(object sender, RoutedEventArgs e)
        {
            double.TryParse(Threshold_TextBox.Text, out double t_val);
            
            threshold_value = t_val;             // ON/OFF判定用の値 (threshold)
        }
    }
}
