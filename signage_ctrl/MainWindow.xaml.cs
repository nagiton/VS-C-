using System;
using System.Collections.Generic;
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
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Kinect;


namespace signage_ctrl
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        //骨格情報のバッファ
        private Skeleton[] skeletonbuffer = null;

        private Joint[] right_wrist = null;
        private double[][] right_wrist_x_pos = null; //[骨格のID][時系列]

        private Joint[] right_shoulder = null;　
        private double[][] right_shoulder_x_pos = null;　//[骨格のID][時系列]

        private double[][] relative_right = null; //[骨格のID][時系列]

        private Joint[] left_wrist = null;
        private double[][] left_wrist_x_pos = null; //[骨格のID][時系列]

        private Joint[] left_shoulder = null;
        private double[][] left_shoulder_x_pos = null;　//[骨格のID][時系列]

        private double[][] relative_left = null; //[骨格のID][時系列]



        //操作があったあとの不感時間
        private int deadtime = 1000;

        //速度を計算するためフレーム数 
        private int frame_num = 10;

        //動作のしきい値
        private double threshold = 0.3;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void open_file(object sender, RoutedEventArgs e)
        {
            // ダイアログのインスタンスを生成
            var dialog = new Microsoft.Win32.OpenFileDialog();

            // ファイルの種類を設定
            dialog.Filter = "全てのファイル (*.*)|*.*";

            // ダイアログを表示する
            if (dialog.ShowDialog() == true)
            {
                // 選択されたファイル名 (ファイルパス) をメッセージボックスに表示
                //System.Windows.MessageBox.Show(dialog.FileName);

                var filename = dialog.FileName;
                System.Diagnostics.Process.Start(filename);


            }
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //キネクトを初期化
            KinectSensor kinect = KinectSensor.KinectSensors[0];


            //骨格ストリームの有効化
            SkeletonStream skelStream = kinect.SkeletonStream;
            skelStream.Enable();

            //バッファの初期化
            skeletonbuffer = new Skeleton[skelStream.FrameSkeletonArrayLength];
            right_wrist = new Joint[skelStream.FrameSkeletonArrayLength];
            left_wrist = new Joint[skelStream.FrameSkeletonArrayLength];

            right_wrist_x_pos = new double[skelStream.FrameSkeletonArrayLength][];
            for (int i = 0; i < skelStream.FrameSkeletonArrayLength; ++i)
            {
                right_wrist_x_pos[i] = new double[frame_num]; 
            }


            left_wrist_x_pos = new double[skelStream.FrameSkeletonArrayLength][];
            for (int i = 0; i < skelStream.FrameSkeletonArrayLength; ++i)
            {
                left_wrist_x_pos[i] = new double[frame_num];
            }


            right_shoulder = new Joint[skelStream.FrameSkeletonArrayLength];
            left_shoulder = new Joint[skelStream.FrameSkeletonArrayLength];

            right_shoulder_x_pos = new double[skelStream.FrameSkeletonArrayLength][];
            for (int i = 0; i < skelStream.FrameSkeletonArrayLength; ++i)
            {
                right_shoulder_x_pos[i] = new double[frame_num];
            }

            left_shoulder_x_pos = new double[skelStream.FrameSkeletonArrayLength][];
            for (int i = 0; i < skelStream.FrameSkeletonArrayLength; ++i)
            {
                left_shoulder_x_pos[i] = new double[frame_num];
            }


            relative_right = new double[skelStream.FrameSkeletonArrayLength][];
            for (int i = 0; i < skelStream.FrameSkeletonArrayLength; ++i)
            {
                relative_right[i] = new double[frame_num];
            }


            relative_left = new double[skelStream.FrameSkeletonArrayLength][];
            for (int i = 0; i < skelStream.FrameSkeletonArrayLength; ++i)
            {
                relative_left[i] = new double[frame_num];
            }



            //イベントハンドラを設定
            kinect.SkeletonFrameReady += handsign_check;


            //キネクトを起動
            kinect.Start();
        }

        private void handsign_check(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    // スケルトンデータを取得する
                    skeletonFrame.CopySkeletonDataTo(skeletonbuffer);

                    for (int i = 0;i<= 5; ++i)
                    {

                        if (skeletonbuffer[i].TrackingState == SkeletonTrackingState.Tracked)
                        {


                            //右手の動作に対する処理
                            //右手首のX座標のデータを取得
                            right_wrist[i] = skeletonbuffer[i].Joints[JointType.WristRight];

                            //手首の座標の配列の更新
                            for (int j = frame_num - 1; j > 0; --j)
                            {
                                right_wrist_x_pos[i][j] = right_wrist_x_pos[i][j - 1];
                            }
                            right_wrist_x_pos[i][0] = right_wrist[i].Position.X;


                            //右肩についても同様
                            right_shoulder[i] = skeletonbuffer[i].Joints[JointType.ShoulderRight];

                            for (int j = frame_num - 1; j > 0; --j)
                            {
                                right_shoulder_x_pos[i][j] = right_shoulder_x_pos[i][j - 1];
                            }
                            right_shoulder_x_pos[i][0] = right_shoulder[i].Position.X;


                            //相対位置を計算
                            for (int j = frame_num - 1; j > 0; --j)
                            {
                                relative_right[i][j] = relative_right[i][j - 1];
                            }

                            relative_right[i][0] = right_wrist_x_pos[i][0] - right_shoulder_x_pos[i][0];



                            //スワイプ判定ロジック
                            //右手首と右肩の相対位置を計算
                            //配列の最初と最後で符号が異なれば動作があったと判定
                            //最初と最後の差がしきい値以上で負なら右から左へのスワイプと判定

                            //スワイプ判定があってからdeadtime msecの間は動作を停止する


                            //右手の左へのスワイプ
                            if (relative_right[i][0] * relative_right[i][frame_num - 1] < 0 && (relative_right[i][0] - relative_right[i][frame_num - 1]) > threshold)
                            {
                                //System.Media.SystemSounds.Beep.Play();
                                SendKeys.SendWait("{DOWN}");
                                System.Threading.Thread.Sleep(deadtime);

                                for (int j = 0; j < 6; ++j)
                                {
                                    relative_right[j] = new double[frame_num];

                                }

                            }

                            //右手の右へのスワイプ
                            if (relative_right[i][0] * relative_right[i][frame_num - 1] < 0 && (relative_right[i][0] - relative_right[i][frame_num - 1])< -1*threshold)
                            {
                                //System.Media.SystemSounds.Beep.Play();
                                SendKeys.SendWait("{UP}");
                                System.Threading.Thread.Sleep(deadtime);

                                for (int j = 0; j < 6; ++j)
                                {
                                    relative_right[j] = new double[frame_num];

                                }

                            }

                            ///////////////////////////////////////////////////////////////////////////

                            //左手の動作に対する処理
                            //右手首のX座標のデータを取得
                            left_wrist[i] = skeletonbuffer[i].Joints[JointType.WristLeft];

                            //手首の座標の配列の更新
                            for (int j = frame_num - 1; j > 0; --j)
                            {
                                left_wrist_x_pos[i][j] = left_wrist_x_pos[i][j - 1];
                            }
                            left_wrist_x_pos[i][0] = left_wrist[i].Position.X;


                            //左肩についても同様
                            left_shoulder[i] = skeletonbuffer[i].Joints[JointType.ShoulderLeft];

                            for (int j = frame_num - 1; j > 0; --j)
                            {
                                left_shoulder_x_pos[i][j] = left_shoulder_x_pos[i][j - 1];
                            }
                            left_shoulder_x_pos[i][0] = left_shoulder[i].Position.X;


                            //相対位置を計算
                            for (int j = frame_num - 1; j > 0; --j)
                            {
                                relative_left[i][j] = relative_left[i][j - 1];
                            }

                            relative_left[i][0] = left_wrist_x_pos[i][0] - left_shoulder_x_pos[i][0];



                            //スワイプ判定ロジック
                            //右手首と右肩の相対位置を計算
                            //配列の最初と最後で符号が異なれば動作があったと判定
                            //最初と最後の差がしきい値以上で負なら右から左へのスワイプと判定

                            //スワイプ判定があってからdeadtime msecの間は動作を停止する


                            //左手の左へのスワイプ
                            if (relative_left[i][0] * relative_left[i][frame_num - 1] < 0 && (relative_left[i][0] - relative_left[i][frame_num - 1]) > threshold)
                            {
                                //System.Media.SystemSounds.Beep.Play();
                                SendKeys.SendWait("{DOWN}");
                                System.Threading.Thread.Sleep(deadtime);

                                for (int j = 0; j < 6; ++j)
                                {
                                    relative_left[j] = new double[frame_num];

                                }

                            }

                            //左手の右へのスワイプ
                            if (relative_left[i][0] * relative_left[i][frame_num - 1] < 0 && (relative_left[i][0] - relative_left[i][frame_num - 1]) < -1 * threshold)
                            {
                                //System.Media.SystemSounds.Beep.Play();
                                SendKeys.SendWait("{UP}");
                                System.Threading.Thread.Sleep(deadtime);

                                for (int j = 0; j < 6; ++j)
                                {
                                    relative_left[j] = new double[frame_num];

                                }

                            }
                        }

                    }

                    



                }
            }   
        }
    }
    
}
