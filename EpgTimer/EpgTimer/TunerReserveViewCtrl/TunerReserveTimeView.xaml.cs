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

namespace EpgTimer.TunerReserveViewCtrl
{
    /// <summary>
    /// TunerReserveTimeView.xaml の相互作用ロジック
    /// </summary>
    public partial class TunerReserveTimeView : UserControl
    {
        public TunerReserveTimeView()
        {
            InitializeComponent();
        }

        public void ClearInfo()
        {
            stackPanel_time.Children.Clear();
        }

        public void SetTime(List<DateTime> timeList, double heightPerHour, List<Brush> brushList)
        {
            ClearInfo();
            //時間の数字は、太さの設定が既定(400)のときは太字、指定があるときはその太さにする
            int timeFontWeight = Settings.Instance.EpgSettingList[0].FontWeightTimeHeader;
            FontWeight hourWeight = timeFontWeight == 400 ? FontWeights.Bold : FontWeight.FromOpenTypeWeight(Math.Max(1, Math.Min(999, timeFontWeight)));
            //フォントは番組表の左端(デフォルトのデザイン)と同じ設定を使う
            EpgTimer.EpgView.EpgHeaderFormat.ApplyTimeHeaderFont(Settings.Instance.EpgSettingList[0], this);
            if (heightPerHour > 1)
            {
                foreach (DateTime time in timeList)
                {
                    // 番組表の左端と同じく、日付と時間を右に90度回転させた1行で表示する(例: 10/17(土) 15)
                    var text = new TextBlock()
                    {
                        Style = (Style)FindResource("AppEpgTimeHeaderDateTextBlockStyle"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        LayoutTransform = new RotateTransform(90)
                    };
                    if (time.Hour % 3 == 0)
                    {
                        text.Inlines.Add(new Run(time.ToString("M\\/d")));
                        if (heightPerHour >= 60)
                        {
                            text.Inlines.Add(new Run(time.ToString("(ddd)")));
                        }
                        text.Inlines.Add(new Run(" "));
                    }
                    text.Inlines.Add(new Run(time.Hour.ToString()) { FontWeight = hourWeight });

                    var grid = new Grid()
                    {
                        Background = brushList[time.Hour],
                        Height = heightPerHour - 1,
                        Margin = new Thickness(1, 1, 1, 0)
                    };
                    //番組表の左端と同じ黒いぼかし(デフォルトのデザインの設定)を入れる
                    EpgSetting epgSetting = Settings.Instance.EpgSettingList[0];
                    Brush textBrush = EpgTimer.EpgView.EpgHeaderFormat.GetTextBrush(epgSetting.TimeTextColor, epgSetting.TimeTextCustColor);
                    if (textBrush != null)
                    {
                        text.Foreground = textBrush;
                    }
                    var host = new Border() { Child = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
                    Color? blurColor = EpgTimer.EpgView.EpgHeaderFormat.GetBlurColor(epgSetting.TimeBlurColorEnabled, epgSetting.TimeBlurColor, epgSetting.TimeBlurCustColor);
                    EpgTimer.EpgView.EpgHeaderFormat.SetTextBlur(host, epgSetting.TimeBlurRadius, textBrush ?? Brushes.White, blurColor);
                    EpgTimer.EpgView.EpgHeaderFormat.SetTextBlur(text, epgSetting.TimeBlurRadius, textBrush ?? Brushes.White, blurColor);
                    grid.Children.Add(host);
                    stackPanel_time.Children.Add(grid);
                }
            }
        }

        private void scrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }
    }
}
