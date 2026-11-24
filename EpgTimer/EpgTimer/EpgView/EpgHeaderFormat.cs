using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EpgTimer.EpgView
{
    /// <summary>
    /// 番組表の「現在」ボタンとサービス名(日付)が並ぶ行のフォントと高さを設定に合わせる
    /// </summary>
    static class EpgHeaderFormat
    {
        /// <summary>
        /// nowButtons(「現在」ボタン。左端用・右端用)には、全体のフォントの設定を適用する(ヘッダー行のフォントの設定は使わない)
        /// </summary>
        public static void Apply(EpgSetting setting, bool weekMode, RowDefinition dateRow, RowDefinition row, Control[] nowButtons, Control view)
        {
            //日付と時間帯の行(上の行)の高さは通常の高さ
            dateRow.Height = (GridLength)Application.Current.FindResource("AppEpgHeaderGridLength");
            //標準モードと1週間モードで別々の設定を使う
            string fontName = weekMode ? setting.FontNameHeaderWeek : setting.FontNameHeader;
            double fontSize = weekMode ? setting.FontSizeHeaderWeek : setting.FontSizeHeader;
            //0(既定)のときは通常の高さ。異常な値で表示が壊れないよう範囲を制限する
            double height = weekMode ? setting.HeaderHeightWeek : setting.HeaderHeight;
            row.Height = height >= 1 && height <= 500 ?
                new GridLength(height) : (GridLength)Application.Current.FindResource("AppEpgHeaderGridLength");

            if (string.IsNullOrWhiteSpace(fontName))
            {
                view.ClearValue(Control.FontFamilyProperty);
            }
            else
            {
                view.FontFamily = CommonManager.CreateFontFamily(fontName);
            }
            //「現在」ボタンは、動作設定の「全体のフォントを設定する」で設定したフォントにする(設定していないときは既定のまま)
            foreach (Control nowButton in nowButtons)
            {
                if (Settings.Instance.SetAppFont && string.IsNullOrWhiteSpace(Settings.Instance.AppFontName) == false)
                {
                    nowButton.FontFamily = CommonManager.CreateFontFamily(Settings.Instance.AppFontName);
                    nowButton.FontSize = Settings.Instance.AppFontSize;
                }
                else
                {
                    nowButton.ClearValue(Control.FontFamilyProperty);
                    nowButton.ClearValue(Control.FontSizeProperty);
                }
            }
            if (fontSize >= 1 && fontSize <= 200)
            {
                view.FontSize = fontSize;
            }
            else
            {
                view.ClearValue(Control.FontSizeProperty);
            }
            int fontWeight = weekMode ? setting.FontWeightHeaderWeek : setting.FontWeightHeader;
            if (fontWeight >= 1 && fontWeight <= 999 && fontWeight != 400)
            {
                view.FontWeight = FontWeight.FromOpenTypeWeight(fontWeight);
            }
            else
            {
                view.ClearValue(Control.FontWeightProperty);
            }
        }

        /// <summary>
        /// 設定値からフォントとサイズを求める。既定のままにするときはfamilyがnull、sizeが0になる
        /// </summary>
        public static void ResolveFont(string fontName, double fontSize, out FontFamily family, out double size)
        {
            family = string.IsNullOrWhiteSpace(fontName) ? null : CommonManager.CreateFontFamily(fontName);
            size = fontSize >= 1 && fontSize <= 200 ? fontSize : 0;
        }

        public static void ApplyFont(Control control, FontFamily family, double size, int weight = 400)
        {
            if (family != null)
            {
                control.FontFamily = family;
            }
            else
            {
                control.ClearValue(Control.FontFamilyProperty);
            }
            if (size > 0)
            {
                control.FontSize = size;
            }
            else
            {
                control.ClearValue(Control.FontSizeProperty);
            }
            if (weight >= 1 && weight <= 999 && weight != 400)
            {
                control.FontWeight = FontWeight.FromOpenTypeWeight(weight);
            }
            else
            {
                control.ClearValue(Control.FontWeightProperty);
            }
        }

        /// <summary>
        /// 番組表の左端(日付と時間)のフォントを設定に合わせる
        /// </summary>
        public static void ApplyTimeHeaderFont(EpgSetting setting, Control timeView)
        {
            FontFamily family;
            double size;
            ResolveFont(setting.FontNameTimeHeader, setting.FontSizeTimeHeader, out family, out size);
            ApplyFont(timeView, family, size, setting.FontWeightTimeHeader);
        }

        /// <summary>
        /// 日付と時間の表示位置(0:両端、1:左端のみ、2:右端のみ)と、時間軸の幅(px)を反映する。
        /// leftColumnControlsは左端の列に置いている部品(「現在」ボタン以外)で、左端を表示しないときは右端の列に移す。
        /// 戻り値は左端の列の幅(高DPI環境での位置合わせの基準)
        /// </summary>
        public static double ApplyTimeHeaderPlace(EpgSetting setting, Grid grid, UIElement leftTimeView, UIElement rightTimeView, UIElement leftNowButton, UIElement rightNowButton, params UIElement[] leftColumnControls)
        {
            bool showRight = setting.TimeHeaderPlace == 0 || setting.TimeHeaderPlace == 2;
            bool showLeft = setting.TimeHeaderPlace != 2;
            GridLength header = (GridLength)Application.Current.FindResource("AppEpgHeaderGridLength");
            //時間軸の幅(px)の指定があれば、既定の幅の代わりに使う(0や空欄のときは既定)
            if (setting.TimeHeaderWidth > 0)
            {
                header = new GridLength(Math.Min(setting.TimeHeaderWidth, 500));
            }
            leftTimeView.Visibility = showLeft ? Visibility.Visible : Visibility.Collapsed;
            rightTimeView.Visibility = showRight ? Visibility.Visible : Visibility.Collapsed;
            //「現在」ボタンも左端・右端それぞれの表示に合わせる
            leftNowButton.Visibility = showLeft ? Visibility.Visible : Visibility.Collapsed;
            rightNowButton.Visibility = showRight ? Visibility.Visible : Visibility.Collapsed;
            grid.ColumnDefinitions[0].Width = showLeft ? header : new GridLength(0);
            grid.ColumnDefinitions[2].Width = showRight ? header : new GridLength(0);
            foreach (UIElement control in leftColumnControls)
            {
                Grid.SetColumn(control, showLeft ? 0 : 2);
            }
            return showLeft ? header.Value : 0;
        }

        /// <summary>
        /// 設定の文字色を求める
        /// </summary>
        public static Brush GetTextBrush(string colorName, uint custColor)
        {
            return ColorDef.CustColorBrush(colorName, custColor);
        }

        /// <summary>
        /// 文字色に対するぼかしの色。黒系の文字は20%グレー、白系の文字は黒
        /// </summary>
        public static Color GetGlowColor(Brush textBrush)
        {
            var solid = textBrush as SolidColorBrush;
            if (solid == null)
            {
                return Colors.Black;
            }
            Color c = solid.Color;
            double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
            return luminance < 0.5 ? Color.FromRgb(204, 204, 204) : Colors.Black;
        }

        /// <summary>
        /// 文字を含む要素に、文字色に合わせた色のぼかしを付ける。radiusが0以下のときは外す
        /// </summary>
        public static void SetTextBlur(UIElement element, double radius, Brush textBrush)
        {
            TimeView.SetBlur(element, Math.Min(radius, 20), GetGlowColor(textBrush));
        }

        /// <summary>
        /// Buttonやコンボボックスの文字(ContentPresenter)にぼかしを付ける。ぼかしの色は現在の文字色に合わせる。
        /// 画面に表示される前はテンプレートがないので、表示された時点で付ける
        /// </summary>
        public static void SetControlTextBlur(Control control, double radius)
        {
            if (control.IsLoaded)
            {
                ApplyControlTextBlur(control, radius);
            }
            else if (radius > 0)
            {
                RoutedEventHandler handler = null;
                handler = (sender, e) =>
                {
                    control.Loaded -= handler;
                    ApplyControlTextBlur(control, radius);
                };
                control.Loaded += handler;
            }
        }

        private static void ApplyControlTextBlur(Control control, double radius)
        {
            control.ApplyTemplate();
            ContentPresenter presenter = FindContentPresenter(control);
            if (presenter != null)
            {
                SetTextBlur(presenter, radius, control.Foreground);
            }
        }

        private static ContentPresenter FindContentPresenter(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                var presenter = child as ContentPresenter;
                if (presenter == null)
                {
                    presenter = FindContentPresenter(child);
                }
                if (presenter != null)
                {
                    return presenter;
                }
            }
            return null;
        }

        private static readonly Dictionary<string, double> textCenterOffsetCache = new Dictionary<string, double>();

        /// <summary>
        /// 文字の字面の中央が、文字を表示する行の中央からどれだけ下にずれるか(px)を返す。
        /// フォントによって、行の上下の余白(ascent/descentや行間)が違うため、枠の中央に置いても文字が上下にずれて見える
        /// </summary>
        private static double GetTextCenterOffset(FontFamily family, FontStyle style, FontWeight weight, FontStretch stretch, double fontSize)
        {
            string key = family.Source + "|" + style + "|" + weight + "|" + stretch + "|" + fontSize.ToString(CultureInfo.InvariantCulture);
            lock (textCenterOffsetCache)
            {
                double cached;
                if (textCenterOffsetCache.TryGetValue(key, out cached))
                {
                    return cached;
                }
            }
            double offset = 0;
            try
            {
                //日本語とアルファベットの両方を含む文字で、実際に使われるフォント(代替フォントを含む)の行の高さと基準線を測る
#pragma warning disable 618
                var formatted = new FormattedText("あA", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                                  new Typeface(family, style, weight, stretch), fontSize, Brushes.Black);
#pragma warning restore 618
                //字面の中央は、基準線からフォントサイズの約0.38倍だけ上にある
                offset = (formatted.Baseline - 0.38 * fontSize) - formatted.Height / 2;
                if (double.IsNaN(offset) || Math.Abs(offset) > fontSize * 0.5)
                {
                    offset = 0;
                }
            }
            catch
            {
            }
            lock (textCenterOffsetCache)
            {
                textCenterOffsetCache[key] = offset;
            }
            return offset;
        }

        private static Transform CreateCenterTransform(double offset)
        {
            if (Math.Abs(offset) < 0.5)
            {
                return Transform.Identity;
            }
            var transform = new TranslateTransform(0, -Math.Round(offset));
            transform.Freeze();
            return transform;
        }

        /// <summary>
        /// rootの中のTextBlockについて、フォントによる上下のずれを補正して、枠の中央に見えるようにする。
        /// TextBlockは枠の中央に置かれている(縦方向が中央)ことが前提
        /// </summary>
        public static void CenterTextBlocks(DependencyObject root)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                var textBlock = child as TextBlock;
                if (textBlock != null)
                {
                    double offset = GetTextCenterOffset(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch, textBlock.FontSize);
                    //TextBlockの余白(Padding)も、文字の位置のずれに含める
                    offset += (textBlock.Padding.Top - textBlock.Padding.Bottom) / 2;
                    textBlock.RenderTransform = CreateCenterTransform(offset);
                }
                var dependencyObject = child as DependencyObject;
                if (dependencyObject != null)
                {
                    CenterTextBlocks(dependencyObject);
                }
            }
        }

        /// <summary>
        /// Buttonやコンボボックスの文字(ContentPresenter)の上下のずれをフォントに合わせて補正する。
        /// 画面に表示される前はテンプレートがないので、表示された時点で補正する
        /// </summary>
        public static void SetControlTextCenter(Control control)
        {
            if (control.IsLoaded)
            {
                ApplyControlTextCenter(control);
            }
            else
            {
                RoutedEventHandler handler = null;
                handler = (sender, e) =>
                {
                    control.Loaded -= handler;
                    ApplyControlTextCenter(control);
                };
                control.Loaded += handler;
            }
        }

        private static void ApplyControlTextCenter(Control control)
        {
            control.ApplyTemplate();
            ContentPresenter presenter = FindContentPresenter(control);
            if (presenter != null)
            {
                double offset = GetTextCenterOffset(control.FontFamily, control.FontStyle, control.FontWeight, control.FontStretch, control.FontSize);
                presenter.RenderTransform = CreateCenterTransform(offset);
            }
        }

        //Gガイド(bangumi.org)の番組表の左端の時間帯の色(0時〜23時)
        private static readonly uint[] GGuideTimeColors = new uint[] {
            0x28289E, 0x2139A5, 0x204C9D, 0x2A6381, 0x34797C, 0x409070, 0x49A351, 0x52B243,
            0x5ABE3C, 0x6CCA3F, 0x82D841, 0xABDE45, 0xDDD345, 0xF4B847, 0xEF9452, 0xED744F,
            0xE45048, 0xD43D53, 0xC03173, 0xA82B8F, 0x8E29A2, 0x7821BB, 0x5920B5, 0x4027A8
        };

        /// <summary>
        /// 番組表の左右端の日付・時刻の背景ブラシを、0時〜23時の24個作る。
        /// TimeColorGGuideのときはGガイド風に1時間ごとに色を変え、そうでないときは従来の4つの時間帯の色を使う。
        /// グラデーション表示の設定(EpgGradationHeader)が有効なときは、グラデーションにする
        /// </summary>
        public static List<Brush> CreateTimeBrushes(EpgSetting setting)
        {
            var colors = new List<Color>();
            if (setting.TimeColorGGuide)
            {
                foreach (uint rgb in GGuideTimeColors)
                {
                    colors.Add(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
                }
            }
            else
            {
                var periodColors = new List<Color>();
                for (int i = 0; i < setting.TimeColorList.Count; i++)
                {
                    periodColors.Add(ColorDef.CustColorBrush(setting.TimeColorList[i], setting.TimeCustColorList[i]).Color);
                }
                for (int hour = 0; hour < 24; hour++)
                {
                    colors.Add(periodColors[Math.Min(hour / 6, periodColors.Count - 1)]);
                }
            }
            var brushes = new List<Brush>();
            foreach (Color color in colors)
            {
                if (setting.EpgGradationHeader)
                {
                    brushes.Add(ColorDef.GradientBrush(color));
                }
                else
                {
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    brushes.Add(brush);
                }
            }
            return brushes;
        }

        //Gガイド(bangumi.org)の番組表のサービス名の背景色(左から順に繰り返す7色)
        private static readonly uint[] GGuideServiceColors = new uint[] {
            0x4397F7, 0x58C06B, 0xE7C040, 0xE68531, 0xD34F3C, 0x9D35D8, 0x3F41CD
        };

        /// <summary>
        /// サービス名の背景ブラシを、7色(青、緑、黄、橙、赤、紫、藍)で作る。サービスの並び順に、この7色を繰り返して使う。
        /// グラデーション表示の設定(EpgGradationHeader)が有効なときは、グラデーションにする
        /// </summary>
        public static List<Brush> CreateServiceCycleBrushes(EpgSetting setting)
        {
            var brushes = new List<Brush>();
            foreach (uint rgb in GGuideServiceColors)
            {
                Color color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                if (setting.EpgGradationHeader)
                {
                    brushes.Add(ColorDef.GradientBrush(color));
                }
                else
                {
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    brushes.Add(brush);
                }
            }
            return brushes;
        }

        /// <summary>
        /// 設定のぼかしの色。指定していない(文字色に合わせて自動にする)ときはnull
        /// </summary>
        public static Color? GetBlurColor(bool enabled, string colorName, uint custColor)
        {
            if (enabled == false)
            {
                return null;
            }
            return ColorDef.CustColorBrush(colorName, custColor).Color;
        }

        /// <summary>
        /// 文字を含む要素にぼかしを付ける。blurColorがnullのときは、文字色に合わせた自動の色にする
        /// </summary>
        public static void SetTextBlur(UIElement element, double radius, Brush textBrush, Color? blurColor)
        {
            TimeView.SetBlur(element, Math.Min(radius, 20), blurColor.HasValue ? blurColor.Value : GetGlowColor(textBrush));
        }
    }
}
