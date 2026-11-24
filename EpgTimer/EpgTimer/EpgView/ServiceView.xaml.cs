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
using System.Windows.Threading;

namespace EpgTimer.EpgView
{
    /// <summary>
    /// ServiceView.xaml の相互作用ロジック
    /// </summary>
    public partial class ServiceView : UserControl
    {
        public event Action<EpgServiceInfo> LeftDoubleClick;
        public event Action<EpgServiceInfo> Click;

        public ServiceView()
        {
            InitializeComponent();
        }

        public void ClearInfo()
        {
            stackPanel_service.Children.Clear();
        }

        private double logoHeight = 0;

        public void SetService(List<EpgServiceInfo> serviceList, double serviceWidth, Brush backgroundBrush, bool isLight, bool isClickLeft, bool showDetail = true, double logoHeight = 0, double blurRadius = 0, Brush textBrush = null, IList<Brush> cycleBrushes = null, Color? blurColor = null)
        {
            this.logoHeight = logoHeight;
            ClearInfo();
            uint tickCountToPreventAccidentalClick = (uint)Environment.TickCount;

            int serviceIndex = 0;
            foreach (EpgServiceInfo info in serviceList)
            {
                //サービス背景を複数色で繰り返すときは、並び順に色を決める(文字は白)
                bool useCycle = cycleBrushes != null && cycleBrushes.Count > 0;
                Brush itemBackground = useCycle ? cycleBrushes[serviceIndex % cycleBrushes.Count] : backgroundBrush;
                bool itemLight = useCycle ? false : isLight;
                serviceIndex++;
                var item = new TextBlock()
                {
                    Style = (Style)FindResource(itemLight ? "AppEpgServiceHeaderLightBackgroundTextBlockStyle" : "AppEpgServiceHeaderTextBlockStyle"),
                    Text = Settings.Instance.GetServiceDisplayName(info.ONID, info.TSID, info.SID, info.service_name),
                    //スタイルは上寄せだが、フォントによって枠の上側に偏って見えるため縦方向は中央にそろえる
                    VerticalAlignment = VerticalAlignment.Center
                };
                //2行目(リモコンキー/ネットワーク名とサービス番号)は設定で表示を切り替える
                if (showDetail == false)
                {
                }
                else if (info.remote_control_key_id != 0)
                {
                    item.Text += "\r\n" + info.remote_control_key_id.ToString();
                }
                else if (info.ONID == 0x000A)
                {
                    item.Text += "\r\n" + info.network_name + " " + (info.SID & 0x3FF).ToString();
                }
                else
                {
                    item.Text += "\r\n" + info.network_name + " " + info.SID.ToString();
                }
                //文字色を指定しているときは既定の色より優先する
                if (textBrush != null)
                {
                    item.Foreground = textBrush;
                }
                //文字の背景のぼかし。文字色が黒系のときは20%グレー、白系のときは黒。文字と枠の両方に影を付ける
                var host = new Border() { Child = item };
                if (blurRadius > 0)
                {
                    Brush effectiveBrush = textBrush ?? (itemLight ? Brushes.Black : Brushes.White);
                    EpgHeaderFormat.SetTextBlur(host, blurRadius, effectiveBrush, blurColor);
                    EpgHeaderFormat.SetTextBlur(item, blurRadius, effectiveBrush, blurColor);
                }
                Grid.SetColumn(host, 1);
                Grid.SetRowSpan(host, 2);
                var grid = new Grid()
                {
                    Background = itemBackground,
                    Margin = new Thickness(1, 1, 1, 1),
                    Width = serviceWidth - 2
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.RowDefinitions.Add(new RowDefinition());
                grid.RowDefinitions.Add(new RowDefinition());

                DispatcherTimer clickTimer = null;
                grid.MouseLeftButtonDown += (sender, e) =>
                {
                    if (e.ClickCount == 1 && clickTimer == null && isClickLeft && Click != null && (uint)e.Timestamp - tickCountToPreventAccidentalClick > 500)
                    {
                        // ダブルクリックと区別するため
                        clickTimer = new DispatcherTimer();
                        clickTimer.Interval = CommonUtil.GetDoubleClickTime() + TimeSpan.FromMilliseconds(100);
                        var tag = (EpgServiceInfo)((FrameworkElement)sender).Tag;
                        clickTimer.Tick += (sender2, e2) =>
                        {
                            clickTimer.Stop();
                            clickTimer = null;
                            if (Click != null)
                            {
                                Click(tag);
                            }
                        };
                        clickTimer.Start();
                    }
                    else if (clickTimer != null)
                    {
                        clickTimer.Stop();
                        clickTimer = null;
                    }
                    if (e.ClickCount == 2 && LeftDoubleClick != null)
                    {
                        LeftDoubleClick((EpgServiceInfo)((FrameworkElement)sender).Tag);
                    }
                };
                if (!isClickLeft)
                {
                    grid.MouseRightButtonUp += (sender, e) =>
                    {
                        if (Click != null && (uint)e.Timestamp - tickCountToPreventAccidentalClick > 500)
                        {
                            Click((EpgServiceInfo)((FrameworkElement)sender).Tag);
                        }
                    };
                }
                grid.Tag = info;
                grid.Children.Add(host);
                stackPanel_service.Children.Add(grid);
            }

            RefreshLogo();
        }

        public void RefreshLogo()
        {
            foreach (Grid grid in stackPanel_service.Children)
            {
                Image logoItem = grid.Children.OfType<Image>().FirstOrDefault();
                if (logoItem != null)
                {
                    grid.Children.Remove(logoItem);
                }
                var info = (EpgServiceInfo)grid.Tag;
                ChSet5Item ch;
                if (ChSet5.Instance.ChList.TryGetValue(CommonManager.Create64Key(info.ONID, info.TSID, info.SID), out ch) && ch.Logo != null)
                {
                    //ロゴの高さは既定では行の高さいっぱい。幅は縦横比を保ち、広がりすぎないよう制限する
                    var image = new Image()
                    {
                        Margin = new Thickness(1, 1, 0, 1),
                        Source = ch.Logo,
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Center,
                        MaxWidth = Math.Max(grid.Width / 2, 1)
                    };
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                    if (logoHeight >= 1 && logoHeight <= 500)
                    {
                        //ピクセル単位で大きさを指定したとき
                        image.Height = logoHeight;
                    }
                    Grid.SetRowSpan(image, 2);
                    grid.Children.Insert(0, image);
                }
            }
        }
    }
}
