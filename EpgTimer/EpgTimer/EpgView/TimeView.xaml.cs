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

namespace EpgTimer.EpgView
{
    /// <summary>
    /// TimeView.xaml の相互作用ロジック
    /// </summary>
    public partial class TimeView : UserControl
    {
        private List<DateTime> canvasTimeList = new List<DateTime>();
        private double canvasHeightPerHour;

        public TimeView()
        {
            InitializeComponent();
        }

        public void ClearMarker()
        {
            foreach (Line item in canvas.Children.OfType<Line>().ToArray())
            {
                canvas.Children.Remove(item);
            }
        }

        public void ClearInfo()
        {
            stackPanel_time.Children.Clear();
            ClearMarker();
            canvasTimeList.Clear();
            canvas.Height = 0;
        }

        /// <summary>
        /// trueのとき、マーカーを右端側に表示する(番組表の右端に置く場合用)
        /// </summary>
        public bool MarkerOnRight { get; set; }

        private double blurRadius = 0;
        private Brush textBrush = null;
        private Color? blurColor = null;

        internal static void SetBlur(UIElement element, double radius)
        {
            SetBlur(element, radius, Colors.Black);
        }

        internal static void SetBlur(UIElement element, double radius, Color color)
        {
            if (radius > 0)
            {
                var effect = new System.Windows.Media.Effects.DropShadowEffect()
                {
                    Color = color,
                    ShadowDepth = 0,
                    BlurRadius = radius,
                    Opacity = 1
                };
                effect.Freeze();
                element.Effect = effect;
            }
            else
            {
                element.ClearValue(UIElement.EffectProperty);
            }
        }

        private void ApplyTextStyle(Border host)
        {
            var text = (TextBlock)host.Child;
            if (textBrush != null)
            {
                text.Foreground = textBrush;
            }
            else
            {
                text.ClearValue(TextBlock.ForegroundProperty);
            }
            //ぼかしの色は文字色に合わせる(既定の文字色は白)
            Brush effectiveBrush = textBrush ?? Brushes.White;
            EpgHeaderFormat.SetTextBlur(host, blurRadius, effectiveBrush, blurColor);
            EpgHeaderFormat.SetTextBlur(text, blurRadius, effectiveBrush, blurColor);
        }

        /// <summary>
        /// 日付と時間の文字色(既定の色にするときはnull)と、文字の背景に入れるぼかしの半径(px、0でなし)を指定する
        /// </summary>
        public void SetTextStyle(Brush brush, double radius, Color? glowColor = null)
        {
            textBrush = brush;
            blurColor = glowColor;
            blurRadius = radius <= 20 ? radius : 20;
            foreach (Border host in stackPanel_time.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<Border>()))
            {
                ApplyTextStyle(host);
            }
        }

        public void SetTime(IEnumerable<DateTime> sortedTimeList, double heightPerHour, bool needTimeOnly, List<Brush> brushList, bool weekMode, int fontWeight = 400)
        {
            ClearInfo();
            //時間の数字は、太さの設定が既定(400)のときは太字、指定があるときはその太さにする
            FontWeight hourWeight = fontWeight == 400 ? FontWeights.Bold : FontWeight.FromOpenTypeWeight(Math.Max(1, Math.Min(999, fontWeight)));
            if (heightPerHour > 1)
            {
                foreach (DateTime time in sortedTimeList)
                {
                    canvasTimeList.Add(time);

                    // 日付と時間は右に90度回転させ、上から下へ読む1行で表示する(例: 10/17(土) 15)
                    var text = new TextBlock()
                    {
                        Style = (Style)FindResource("AppEpgTimeHeaderDateTextBlockStyle"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        LayoutTransform = new RotateTransform(90)
                    };
                    if (weekMode == false && (time.Hour % 3 == 0 || needTimeOnly))
                    {
                        text.Inlines.Add(new Run(time.ToString("M\\/d")));
                        if (heightPerHour >= 60)
                        {
                            //曜日は土日の色分けや太字にせず、他の文字と同じ白にする
                            text.Inlines.Add(new Run(time.ToString("(ddd)")));
                        }
                        text.Inlines.Add(new Run(" "));
                    }
                    //時間は日付と同じ大きさ
                    text.Inlines.Add(new Run(time.Hour.ToString()) { FontWeight = hourWeight });

                    var grid = new Grid()
                    {
                        Background = brushList[time.Hour],
                        Height = heightPerHour - 1,
                        Margin = new Thickness(1, 1, 1, 0)
                    };
                    //文字を黒でぼかす。文字と、文字を包む枠の両方に影を付けると、背景が暗くなりすぎず輪郭がはっきりする
                    var host = new Border() { Child = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
                    ApplyTextStyle(host);
                    grid.Children.Add(host);
                    stackPanel_time.Children.Add(grid);
                }
                canvasHeightPerHour = heightPerHour;
                canvas.Height = canvasHeightPerHour * canvasTimeList.Count + stackPanel_time.Margin.Bottom;
            }
        }

        public void AddMarker(IEnumerable<KeyValuePair<DateTime, TimeSpan>> timeRanges, Brush brush)
        {
            if (canvasTimeList.Count > 0)
            {
                var yRanges = new List<Tuple<double, double>>();
                foreach (KeyValuePair<DateTime, TimeSpan> timeRange in timeRanges)
                {
                    // 時間の範囲をY軸の範囲に変換する
                    DateTime startTime = timeRange.Key;
                    int index = canvasTimeList.BinarySearch(new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0));
                    double y1 = canvasHeightPerHour * (index < 0 ? ~index : index + (startTime - canvasTimeList[index]).TotalMinutes / 60);
                    DateTime endTime = startTime + timeRange.Value;
                    index = canvasTimeList.BinarySearch(new DateTime(endTime.Year, endTime.Month, endTime.Day, endTime.Hour, 0, 0));
                    double y2 = canvasHeightPerHour * (index < 0 ? ~index : index + (endTime - canvasTimeList[index]).TotalMinutes / 60);
                    if (y1 < y2)
                    {
                        yRanges.Add(new Tuple<double, double>(y1, y2));
                    }
                }
                yRanges.Sort();

                var blurEffect = new System.Windows.Media.Effects.DropShadowEffect();
                blurEffect.BlurRadius = 10;
                blurEffect.ShadowDepth = 2;
                blurEffect.Freeze();
                for (int i = 0; i < yRanges.Count(); i++)
                {
                    var item = new Line();
                    if (MarkerOnRight)
                    {
                        item.X1 = 0;
                        item.X2 = 0;
                        Canvas.SetRight(item, 5);
                    }
                    else
                    {
                        item.X1 = 5;
                        item.X2 = 5;
                    }
                    item.Y1 = yRanges[i].Item1;
                    // 重なっていればつなげる
                    double y2 = yRanges[i].Item2;
                    for (; i + 1 < yRanges.Count() && y2 >= yRanges[i + 1].Item1; i++)
                    {
                        y2 = Math.Max(y2, yRanges[i + 1].Item2);
                    }
                    item.Y2 = y2;
                    item.Stroke = brush;
                    item.StrokeThickness = 3;
                    item.Effect = blurEffect;
                    canvas.Children.Add(item);
                    Canvas.SetZIndex(item, 10);
                }
            }
        }

        private void scrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }
    }
}
