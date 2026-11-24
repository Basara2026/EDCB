using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Input;

namespace EpgTimer.EpgView
{
    /// <summary>
    /// 番組表記号([字]など)の置換用の画像
    /// </summary>
    static class EpgSymbolImages
    {
        //記号の文字と画像ファイルの対応(括弧の中の文字。「SS」のように2文字のものもある)
        private static readonly string[][] symbolFiles = new string[][]
        {
            new string[] { "デ", "data_64.png" },
            new string[] { "字", "subtitle_64.png" },
            new string[] { "解", "commentary_64.png" },
            new string[] { "二", "bilingual_64.png" },
            new string[] { "多", "multiplex_64.png" },
            new string[] { "双", "interactive_64.png" },
            new string[] { "新", "new_64.png" },
            new string[] { "終", "final_64.png" },
            new string[] { "再", "rerun_64.png" },
            new string[] { "SS", "surround_64.png" },
            new string[] { "映", "movie_64.png" },
            new string[] { "生", "live_64.png" },
            new string[] { "初", "first_64.png" },
            new string[] { "無", "free_64.png" },
            new string[] { "手", "sign_64.png" },
            new string[] { "日", "japanese_64.png" },
            new string[] { "英", "english_64.png" },
            new string[] { "吹", "dubbing_64.png" },
        };
        //「[ ]」「［ ］」「【 】」に加えて、丸括弧「( )」「（ ）」で書かれていても画像にする記号
        //(丸括弧は日付の「(日)」のように記号以外でも使われるので、必要なものだけにしている)
        private static readonly string[] roundBracketSymbols = new string[] { "字", "二", "再", "日" };
        //四角で囲まれた1文字の記号(Unicodeの囲み文字)で表記されている場合の対応
        private static readonly KeyValuePair<int, string>[] enclosedSymbols = new KeyValuePair<int, string>[]
        {
            new KeyValuePair<int, string>(0x1F210, "手"), new KeyValuePair<int, string>(0x1F211, "字"),
            new KeyValuePair<int, string>(0x1F212, "双"), new KeyValuePair<int, string>(0x1F213, "デ"),
            new KeyValuePair<int, string>(0x1F214, "二"), new KeyValuePair<int, string>(0x1F215, "多"),
            new KeyValuePair<int, string>(0x1F216, "解"), new KeyValuePair<int, string>(0x1F219, "映"),
            new KeyValuePair<int, string>(0x1F21A, "無"), new KeyValuePair<int, string>(0x1F21E, "再"),
            new KeyValuePair<int, string>(0x1F21F, "新"), new KeyValuePair<int, string>(0x1F220, "初"),
            new KeyValuePair<int, string>(0x1F221, "終"), new KeyValuePair<int, string>(0x1F222, "生"),
            new KeyValuePair<int, string>(0x1F225, "吹"),
        };
        private const string OpenBrackets = "[［【(（";
        private static Dictionary<string, ImageSource> images;

        /// <summary>
        /// 括弧も含めた記号の文字列(「[字]」「(日)」など)から画像を引く辞書。読み込めなかった画像は含まない
        /// </summary>
        public static Dictionary<string, ImageSource> Get()
        {
            if (images == null)
            {
                var dict = new Dictionary<string, ImageSource>();
                var errors = new List<string>();
                foreach (string[] file in symbolFiles)
                {
                    ImageSource image = null;
                    try
                    {
                        image = Load(new Uri("pack://application:,,,/Resources/EpgSymbols/" + file[1]));
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            //実行ファイルと同じ場所の Resources\EpgSymbols にあれば、それを使う
                            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "EpgSymbols", file[1]);
                            image = Load(new Uri(path));
                        }
                        catch
                        {
                            //読み込めない記号は画像にしない(文字のまま表示する)
                            errors.Add(file[1] + " : " + ex.GetType().Name + " " + ex.Message);
                        }
                    }
                    if (image == null)
                    {
                        continue;
                    }
                    string name = file[0];
                    dict["[" + name + "]"] = image;
                    dict["［" + name + "］"] = image;
                    dict["【" + name + "】"] = image;
                    if (Array.IndexOf(roundBracketSymbols, name) >= 0)
                    {
                        dict["(" + name + ")"] = image;
                        dict["（" + name + "）"] = image;
                    }
                    if (name == "SS")
                    {
                        //全角で表記されている場合も同じ画像にする
                        dict["[ＳＳ]"] = image;
                        dict["［ＳＳ］"] = image;
                        dict["【ＳＳ】"] = image;
                    }
                }
                images = dict;
                if (errors.Count > 0)
                {
                    //何も表示されずに原因が分からなくならないよう、一度だけ知らせる
                    //(描画中はメッセージボックスを出せないので、処理が落ち着いてから出す)
                    string text = "番組表記号の画像を読み込めませんでした。\r\n" + string.Join("\r\n", errors.ToArray());
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        MessageBox.Show(text, "EpgTimer", MessageBoxButton.OK, MessageBoxImage.Warning)),
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
            return images;
        }

        private static ImageSource Load(Uri uri)
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// text[i]から始まる番組表記号を探す。見つかったら画像と、記号が使っている文字数を返す。
        /// 「[字]」「［字］」「【字】」(「SS」のように2文字のものも)と、一部の丸括弧「(日)」など、
        /// 四角で囲まれた1文字の記号に対応する
        /// </summary>
        public static bool TryMatch(string text, int i, Dictionary<string, ImageSource> dict, out ImageSource image, out int length)
        {
            image = null;
            length = 0;
            char open = text[i];
            if (OpenBrackets.IndexOf(open) >= 0)
            {
                for (int len = 1; len <= 2; len++)
                {
                    if (i + len + 1 < text.Length && dict.TryGetValue(text.Substring(i, len + 2), out image))
                    {
                        length = len + 2;
                        return true;
                    }
                }
            }
            else if (char.IsHighSurrogate(open) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                int code = char.ConvertToUtf32(open, text[i + 1]);
                foreach (KeyValuePair<int, string> enclosed in enclosedSymbols)
                {
                    if (enclosed.Key == code && dict.TryGetValue("[" + enclosed.Value + "]", out image))
                    {
                        length = 2;
                        return true;
                    }
                }
            }
            image = null;
            return false;
        }
    }

    class EpgViewPanel : FrameworkElement
    {
        public class ItemFont
        {
            public bool NoCache { get; private set; }
            public GlyphTypeface GlyphType { get; private set; }
            public GlyphTypeface FallbackGlyphType { get; private set; }
            public ushort[] GlyphIndexCache { get; private set; }
            public float[] GlyphWidthCache { get; private set; }

            /// <summary>
            /// weightはフォントの太さ(1〜999、400が標準、700が太字)。フォントに用意されている太さのうち、近いものが使われる
            /// </summary>
            public ItemFont(string familyName, int weight, bool noCache)
            {
                NoCache = noCache;
                FontWeight fontWeight = FontWeight.FromOpenTypeWeight(Math.Max(1, Math.Min(999, weight)));
                GlyphTypeface glyphType;
                int fallback = familyName.IndexOf(',');
                if ((new Typeface(CommonManager.CreateFontFamily(fallback < 0 ? familyName : familyName.Remove(fallback).Trim()),
                                  FontStyles.Normal,
                                  fontWeight,
                                  FontStretches.Normal)).TryGetGlyphTypeface(out glyphType))
                {
                    GlyphType = glyphType;
                }
                if (fallback >= 0 && (new Typeface(CommonManager.CreateFontFamily(familyName.Substring(fallback + 1).Trim()),
                                                   FontStyles.Normal,
                                                   fontWeight,
                                                   FontStretches.Normal)).TryGetGlyphTypeface(out glyphType))
                {
                    if (GlyphType == null)
                    {
                        GlyphType = glyphType;
                    }
                    else
                    {
                        FallbackGlyphType = glyphType;
                    }
                }
                if (GlyphType == null && (new Typeface(SystemFonts.MessageFontFamily,
                                                       FontStyles.Normal,
                                                       fontWeight,
                                                       FontStretches.Normal)).TryGetGlyphTypeface(out glyphType))
                {
                    GlyphType = glyphType;
                }
            }
            public void PrepareCache()
            {
                if (NoCache == false && GlyphIndexCache == null)
                {
                    GlyphIndexCache = new ushort[ushort.MaxValue + 1];
                    GlyphWidthCache = new float[ushort.MaxValue + 1];
                }
            }
            public void ClearCache()
            {
                GlyphIndexCache = null;
                GlyphWidthCache = null;
            }
        }

        public static readonly DependencyProperty BackgroundProperty =
            Panel.BackgroundProperty.AddOwner(typeof(EpgViewPanel));
        private double _borderLeftSize;
        private double _borderTopSize;
        private List<List<Tuple<Brush, GlyphRun>>> textDrawLists;
        private List<List<Tuple<ImageSource, Rect>>> imageDrawLists;
        public Brush Background
        {
            set { SetValue(BackgroundProperty, value); }
            get { return (Brush)GetValue(BackgroundProperty); }
        }

        public List<ProgramViewItem> Items
        {
            get;
            private set;
        }

        /// <summary>
        /// タイトルの文字の背景にぼかしを入れる半径(px)。0のときはぼかさない。
        /// 黒系の文字は20%グレー、白系の文字は黒でぼかす
        /// </summary>
        public double TitleBlurRadius
        {
            get;
            set;
        }

        /// <summary>
        /// タイトル以外の文字の背景にぼかしを入れる半径(px)。0のときはぼかさない
        /// </summary>
        public double BodyBlurRadius
        {
            get;
            set;
        }

        private Brush titleTextBrush = null;

        /// <summary>
        /// 各番組の左端にジャンル別背景色の帯を表示し、その中に時間(分)を表示するモード(Gガイド風)。
        /// 帯の右側の背景は、ジャンル別背景色を白に近づけた(白に20%の濃さで重ねた)色にする
        /// </summary>
        public bool BandMode
        {
            get;
            set;
        }

        /// <summary>
        /// ダークモード。番組名と番組内容の背景を黒(#000000)にする(帯は、ジャンル別背景色のまま)
        /// </summary>
        public bool DarkMode
        {
            get;
            set;
        }

        /// <summary>
        /// 時間(分)の文字幅に帯の幅を自動で合わせられないときの、帯の幅(px)
        /// </summary>
        public double BandWidth
        {
            get;
            set;
        } = 34;

        /// <summary>
        /// 帯の中の時間(分)の文字の背景にぼかしを入れる半径(px)
        /// </summary>
        public double BandBlurRadius
        {
            get;
            set;
        }

        /// <summary>
        /// 帯のモードで、帯と右側の背景をグラデーションにするかどうか(「グラデーション表示を行う」の「番組」の設定)。
        /// このときContentBrushListには、グラデーションにしていない単色のブラシを渡す
        /// </summary>
        public bool BandGradation
        {
            get;
            set;
        }

        /// <summary>
        /// パネルが描く内容の種類。本物のぼかし(BlurEffect)を使うときは、背景、ぼかしの層(タイトル、タイトル以外、帯の中の時間)、
        /// くっきりした文字の層を、別々のパネルに分けて重ねる(ぼかしの層にだけエフェクトをかける)
        /// </summary>
        public enum PanelLayer
        {
            All,
            Background,
            TitleGlow,
            BodyGlow,
            BandGlow,
            Text
        }

        public PanelLayer Layer
        {
            get;
            set;
        }

        /// <summary>
        /// タイトルの文字の背景のぼかしの色。nullのときは文字色に合わせて自動(黒系の文字は20%グレー、白系の文字は黒)
        /// </summary>
        public Color? TitleGlowColor
        {
            get;
            set;
        }

        /// <summary>
        /// タイトル以外の文字の背景のぼかしの色。nullのときは自動
        /// </summary>
        public Color? BodyGlowColor
        {
            get;
            set;
        }

        private Brush bandTextBrush = null;
        private Brush bodyTextBrush = null;
        private double bandWidthPx = 0;

        /// <summary>
        /// 文字の配置の計算結果を、別の層のパネルと共有する(Initializeの計算は1回だけにする)
        /// </summary>
        public void CopyLayoutFrom(EpgViewPanel source)
        {
            Items = source.Items;
            ContentBrushList = source.ContentBrushList;
            Background = source.Background;
            FilteredAlpha = source.FilteredAlpha;
            textDrawLists = source.textDrawLists;
            imageDrawLists = source.imageDrawLists;
            _borderLeftSize = source._borderLeftSize;
            _borderTopSize = source._borderTopSize;
            titleTextBrush = source.titleTextBrush;
            bandTextBrush = source.bandTextBrush;
            bodyTextBrush = source.bodyTextBrush;
            bandWidthPx = source.bandWidthPx;
            BandMode = source.BandMode;
            DarkMode = source.DarkMode;
            BandGradation = source.BandGradation;
            TitleGlowColor = source.TitleGlowColor;
            BodyGlowColor = source.BodyGlowColor;
            LastItemRenderTextHeight = source.LastItemRenderTextHeight;
        }

        public byte FilteredAlpha
        {
            get;
            private set;
        }

        public List<Brush> ContentBrushList
        {
            get;
            private set;
        }

        public double LastItemRenderTextHeight
        {
            get;
            private set;
        }

        public void Initialize(List<ProgramViewItem> items, double borderLeftSize, double borderTopSize,
                               bool drawHour, bool isTitleIndent, bool extInfoMode,
                               Dictionary<char, List<KeyValuePair<string, string>>> replaceDictionaryTitle,
                               Dictionary<char, List<KeyValuePair<string, string>>> replaceDictionaryNormal,
                               ItemFont itemFontTitle, ItemFont itemFontNormal, double sizeTitle, double sizeNormal, Brush brushTitle, Brush brushNormal,
                               byte filteredAlpha, List<Brush> contentBrushList,
                               Dictionary<string, ImageSource> symbolImages = null)
        {
            LastItemRenderTextHeight = 0;
            textDrawLists = null;
            imageDrawLists = null;
            //タイトルとタイトル以外を文字色の参照で見分けるため、同じブラシのときは複製して別のものにする
            if (ReferenceEquals(brushTitle, brushNormal))
            {
                brushNormal = brushNormal.Clone();
            }
            titleTextBrush = brushTitle;
            bodyTextBrush = brushNormal;
            //帯の中の時間(分)の文字色は白(タイトルなどとは別のブラシにして見分ける)
            bandTextBrush = BandMode ? Brushes.White.Clone() : null;
            bandWidthPx = 0;
            Items = items;
            FilteredAlpha = filteredAlpha;
            ContentBrushList = contentBrushList;
            var ps = PresentationSource.FromVisual(Application.Current.MainWindow);
            if (ps != null && itemFontTitle.GlyphType != null && itemFontNormal.GlyphType != null)
            {
                Matrix m = ps.CompositionTarget.TransformToDevice;
                textDrawLists = new List<List<Tuple<Brush, GlyphRun>>>(Items.Count);
                imageDrawLists = new List<List<Tuple<ImageSource, Rect>>>(Items.Count);
                _borderLeftSize = borderLeftSize;
                _borderTopSize = borderTopSize;
                itemFontTitle.PrepareCache();
                itemFontNormal.PrepareCache();

                //ProgramViewItemの座標系は番組表基準なので、この時点でCanvas.SetLeft()によりEpgViewPanel自身の座標を添付済みでなければならない
                double selfLeft = Canvas.GetLeft(this);
                sizeTitle = Math.Max(sizeTitle, 1);
                sizeNormal = Math.Max(sizeNormal, 1);
                double indentTitle = sizeTitle * 1.7;
                if (BandMode)
                {
                    //帯の幅は、時間(分)の文字幅に合わせる。文字幅を測れないときは設定の幅にする
                    double minuteWidth = MeasureTextWidth(itemFontTitle, "00", sizeTitle * 0.95);
                    double hourWidth = drawHour ? MeasureTextWidth(itemFontTitle, "00:", sizeTitle * 0.8) : 0;
                    bandWidthPx = minuteWidth > 0 && hourWidth >= 0 ? Math.Ceiling(Math.Max(minuteWidth, hourWidth)) + 6 : Math.Max(BandWidth, 1);
                    indentTitle = bandWidthPx + 3;
                    //番組内容は番組名の位置にそろえる
                    isTitleIndent = true;
                }
                double indentNormal = isTitleIndent ? indentTitle : 2;
                Brush timeBrush = BandMode ? bandTextBrush : brushTitle;

                foreach (ProgramViewItem info in Items)
                {
                    var textDrawList = new List<Tuple<Brush, GlyphRun>>();
                    textDrawLists.Add(textDrawList);
                    var imageDrawList = new List<Tuple<ImageSource, Rect>>();
                    imageDrawLists.Add(imageDrawList);

                    double innerLeft = info.LeftPos + borderLeftSize / 2;
                    //0.26は細枠線での微調整
                    double innerTop = info.TopPos + borderTopSize / 2 - 0.26;
                    double innerWidth = info.Width - borderLeftSize;
                    double innerHeight = info.Height - borderTopSize;
                    double useHeight = 0;

                    if (drawHour && isTitleIndent)
                    {
                        //時
                        string hour = (info.EventInfo.StartTimeFlag == 0 ? "?" : info.EventInfo.start_time.Hour.ToString()) + ":";
                        RenderText(hour, textDrawList, itemFontTitle, sizeTitle * 0.8, innerWidth - 1, innerHeight,
                                   GetTimeTextLeft(itemFontTitle, hour, sizeTitle * 0.8, innerLeft), innerTop, out useHeight, timeBrush, m, selfLeft);
                    }
                    //分
                    string min = (info.EventInfo.StartTimeFlag == 0 ? "?" : info.EventInfo.start_time.Minute.ToString("d02"));
                    RenderText(min, textDrawList, itemFontTitle, sizeTitle * 0.95, innerWidth - 1, innerHeight,
                               GetTimeTextLeft(itemFontTitle, min, sizeTitle * 0.95, innerLeft), innerTop + useHeight, out useHeight, timeBrush, m, selfLeft);

                    //右端の余白。帯のモードでは、帯の右の余白(3px)と同じにする。
                    //さらに、全角文字を敷き詰めたときに右端に残る端数の半分を、左側にも足して、左右の余白をそろえる
                    double rightMargin = BandMode ? 3 : sizeTitle * 0.5;
                    double textShift = 0;
                    if (BandMode)
                    {
                        double titleAvailable = innerWidth - indentTitle - rightMargin;
                        double titleChars = Math.Floor(titleAvailable / sizeTitle);
                        if (titleChars >= 1)
                        {
                            textShift = Math.Floor((titleAvailable - titleChars * sizeTitle) / 2);
                        }
                    }

                    //タイトル
                    string title = info.EventInfo.ShortInfo == null ? "" : info.EventInfo.ShortInfo.event_name;
                    if (replaceDictionaryTitle != null)
                    {
                        title = CommonManager.ReplaceText(title, replaceDictionaryTitle);
                    }
                    if (RenderText(title.Length > 0 ? title : " ", textDrawList, itemFontTitle, sizeTitle,
                                   innerWidth - rightMargin - indentTitle - textShift, innerHeight,
                                   innerLeft + indentTitle + textShift, innerTop, out useHeight, brushTitle, m, selfLeft, imageDrawList, symbolImages) == false)
                    {
                        info.TitleDrawErr = true;
                        LastItemRenderTextHeight = info.Height;
                        continue;
                    }
                    if (info.Height < sizeTitle)
                    {
                        //高さ足りない
                        info.TitleDrawErr = true;
                    }
                    LastItemRenderTextHeight = useHeight + sizeNormal * 0.5;

                    if (info.EventInfo.ShortInfo != null)
                    {
                        //説明
                        string detail = info.EventInfo.ShortInfo.text_char;
                        //詳細
                        detail += extInfoMode == false || info.EventInfo.ExtInfo == null ? "" :
                                      "\r\n\r\n" + CommonManager.TrimHyphenSpace(info.EventInfo.ExtInfo.text_char);
                        if (replaceDictionaryNormal != null)
                        {
                            detail = CommonManager.ReplaceText(detail, replaceDictionaryNormal);
                        }
                        RenderText(detail, textDrawList, itemFontNormal, sizeNormal,
                                   innerWidth - rightMargin - indentNormal - textShift, innerHeight - LastItemRenderTextHeight,
                                   innerLeft + indentNormal + textShift, innerTop + LastItemRenderTextHeight, out useHeight, brushNormal, m, selfLeft);
                        LastItemRenderTextHeight += useHeight + sizeNormal * 0.25;
                    }
                    LastItemRenderTextHeight = Math.Floor(LastItemRenderTextHeight + borderTopSize + sizeNormal * 0.5);
                    LastItemRenderTextHeight = Math.Min(LastItemRenderTextHeight, info.Height);
                }
            }
        }

        /// <summary>
        /// 文字列の幅(px)を測る。測れないとき(フォントにグリフがないとき)は-1を返す
        /// </summary>
        private static double MeasureTextWidth(ItemFont itemFont, string text, double fontSize)
        {
            double total = 0;
            foreach (char ch in text)
            {
                GlyphTypeface glyphType = itemFont.GlyphType;
                ushort glyphIndex;
                if (glyphType.CharacterToGlyphMap.TryGetValue(ch, out glyphIndex) == false)
                {
                    glyphType = itemFont.FallbackGlyphType;
                    if (glyphType == null || glyphType.CharacterToGlyphMap.TryGetValue(ch, out glyphIndex) == false)
                    {
                        return -1;
                    }
                }
                double advance;
                if (glyphType.AdvanceWidths.TryGetValue(glyphIndex, out advance) == false)
                {
                    return -1;
                }
                total += advance * fontSize;
            }
            return total;
        }

        /// <summary>
        /// 時間(分)の文字の左端の位置。帯のモードでは、帯の中央に置く
        /// </summary>
        private double GetTimeTextLeft(ItemFont itemFont, string text, double fontSize, double innerLeft)
        {
            if (BandMode)
            {
                double width = MeasureTextWidth(itemFont, text, fontSize);
                if (width > 0)
                {
                    return innerLeft + Math.Max((bandWidthPx - width) / 2, 1);
                }
            }
            return innerLeft + 1;
        }

        /// <summary>
        /// ジャンル別背景色を白に近づけた色(白の背景に透過20%で重ねた色)
        /// </summary>
        private static Color GetPaleColor(Color c)
        {
            return Color.FromRgb((byte)Math.Round(c.R * 0.2 + 255 * 0.8), (byte)Math.Round(c.G * 0.2 + 255 * 0.8), (byte)Math.Round(c.B * 0.2 + 255 * 0.8));
        }

        private static readonly Dictionary<Color, Brush> gradientBrushCache = new Dictionary<Color, Brush>();

        private static Brush GetGradientBrush(Color color)
        {
            Brush gradient;
            lock (gradientBrushCache)
            {
                if (gradientBrushCache.TryGetValue(color, out gradient) == false)
                {
                    LinearGradientBrush brush = ColorDef.GradientBrush(color);
                    brush.Freeze();
                    gradient = brush;
                    gradientBrushCache[color] = gradient;
                }
            }
            return gradient;
        }

        private static readonly Dictionary<Color, Brush> paleBrushCache = new Dictionary<Color, Brush>();

        /// <summary>
        /// ジャンル別背景色を白に近づけた色(白の背景に透過20%で重ねた色)のブラシを返す
        /// </summary>
        private static Brush GetPaleBrush(Brush brush)
        {
            var solid = brush as SolidColorBrush;
            if (solid == null)
            {
                return brush;
            }
            Brush pale;
            lock (paleBrushCache)
            {
                if (paleBrushCache.TryGetValue(solid.Color, out pale) == false)
                {
                    pale = new SolidColorBrush(GetPaleColor(solid.Color));
                    pale.Freeze();
                    paleBrushCache[solid.Color] = pale;
                }
            }
            return pale;
        }

        private static readonly Dictionary<Color, Brush[]> glowByColorCache = new Dictionary<Color, Brush[]>();

        /// <summary>
        /// 指定した色の、重ね描き用の半透明のブラシ(内側用と外側用)を返す
        /// </summary>
        private static Brush[] GetGlowBrushesByColor(Color color)
        {
            Brush[] brushes;
            lock (glowByColorCache)
            {
                if (glowByColorCache.TryGetValue(color, out brushes) == false)
                {
                    brushes = new Brush[] {
                        new SolidColorBrush(Color.FromArgb(128, color.R, color.G, color.B)),
                        new SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B))
                    };
                    foreach (Brush b in brushes)
                    {
                        b.Freeze();
                    }
                    glowByColorCache[color] = brushes;
                }
            }
            return brushes;
        }

        /// <summary>
        /// 文字のぼかしの色。指定があればその色、なければ文字色に合わせた自動の色(黒系の文字は20%グレー、白系の文字は黒)。
        /// 文字色が単色でなく、指定もないときはnull
        /// </summary>
        private Color? ResolveGlowColor(Brush textBrush)
        {
            Color? specified = ReferenceEquals(textBrush, titleTextBrush) ? TitleGlowColor :
                               ReferenceEquals(textBrush, bandTextBrush) ? (Color?)null : BodyGlowColor;
            if (specified.HasValue)
            {
                return specified;
            }
            var solid = textBrush as SolidColorBrush;
            if (solid == null)
            {
                return null;
            }
            Color c = solid.Color;
            double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
            return luminance < 0.5 ? Color.FromRgb(204, 204, 204) : Colors.Black;
        }

        /// <summary>
        /// ぼかしの層(TitleGlow、BodyGlow、BandGlow)にかける影の色。ぼかしの色の指定があればその色、なければ文字色に合わせた自動の色。
        /// 求められないときはnull
        /// </summary>
        public Color? GetLayerGlowColor(PanelLayer layer)
        {
            switch (layer)
            {
                case PanelLayer.TitleGlow:
                    return ResolveGlowColor(titleTextBrush);
                case PanelLayer.BodyGlow:
                    return ResolveGlowColor(bodyTextBrush);
                case PanelLayer.BandGlow:
                    return ResolveGlowColor(bandTextBrush);
            }
            return null;
        }

        private bool RunBelongsToLayer(Brush runBrush, PanelLayer layer)
        {
            switch (layer)
            {
                case PanelLayer.TitleGlow:
                    return ReferenceEquals(runBrush, titleTextBrush);
                case PanelLayer.BandGlow:
                    return ReferenceEquals(runBrush, bandTextBrush);
                case PanelLayer.BodyGlow:
                    return ReferenceEquals(runBrush, titleTextBrush) == false && ReferenceEquals(runBrush, bandTextBrush) == false;
            }
            return false;
        }

        private static GlyphRun CreateGlyphRun(double x, double y, Matrix m, double selfLeft,
                                               GlyphTypeface glyphType, double fontSize, List<ushort> indexes, List<double> widths)
        {
            //originを物理ピクセルに合わせる。selfLeftはレンダリング時に加算されるので混ぜてはいけない
            Point origin = new Point(Math.Ceiling(x * m.M11) / m.M11 - selfLeft, Math.Ceiling(y * m.M22) / m.M22);
            return new GlyphRun(glyphType, 0, false, fontSize, indexes, origin, widths, null, null, null, null, null, null);
        }

        public static bool RenderText(string text, List<Tuple<Brush, GlyphRun>> textDrawList, ItemFont itemFont, double fontSize,
                                      double maxWidth, double maxHeight, double x, double y,
                                      out double totalHeight, Brush fontColor, Matrix m, double selfLeft,
                                      List<Tuple<ImageSource, Rect>> imageDrawList = null, Dictionary<string, ImageSource> symbolImages = null)
        {
            totalHeight = 0;

            for (int i = 0; i < text.Length; )
            {
                if (totalHeight > maxHeight)
                {
                    //次の行無理
                    return false;
                }
                //行間は2行目から適用する
                totalHeight += totalHeight == 0 ? fontSize : fontSize * 1.2;
                List<ushort> glyphIndexes = new List<ushort>();
                List<double> advanceWidths = new List<double>();
                double totalWidth = 0;
                double originWidth = 0;
                GlyphTypeface currentGlyphType = null;
                for (; i < text.Length && text[i] != '\r' && text[i] != '\n'; i++)
                {
                    //番組表記号([字]など)は、画像で置き換えて表示する
                    ImageSource symbolImage;
                    int symbolLength;
                    if (symbolImages != null && imageDrawList != null && EpgSymbolImages.TryMatch(text, i, symbolImages, out symbolImage, out symbolLength))
                    {
                        double symbolAdvance = fontSize * 1.5;
                        if (glyphIndexes.Count > 0)
                        {
                            textDrawList.Add(new Tuple<Brush, GlyphRun>(fontColor, CreateGlyphRun(
                                x + originWidth, y + totalHeight, m, selfLeft, currentGlyphType, fontSize, glyphIndexes, advanceWidths)));
                            glyphIndexes = new List<ushort>();
                            advanceWidths = new List<double>();
                        }
                        //次の文字は新しい文字列として配置する(originWidthはフォントが変わった扱いで更新される)
                        currentGlyphType = null;
                        if (totalWidth > 0 && totalWidth + symbolAdvance > maxWidth)
                        {
                            if (totalHeight > maxHeight)
                            {
                                //次の行無理
                                return false;
                            }
                            totalHeight += fontSize * 1.2;
                            originWidth = totalWidth = 0;
                        }
                        //文字より一回り大きい正方形(見やすいように1.3倍)。ベースラインから上に文字の高さの分だけ、少し下にはみ出させる
                        double symbolSize = Math.Round(fontSize * 1.3 * m.M11) / m.M11;
                        double symbolLeft = Math.Ceiling((x + totalWidth + fontSize * 0.05) * m.M11) / m.M11 - selfLeft;
                        double symbolTop = Math.Ceiling((y + totalHeight - fontSize * 1.1) * m.M22) / m.M22;
                        imageDrawList.Add(new Tuple<ImageSource, Rect>(symbolImage, new Rect(symbolLeft, symbolTop, symbolSize, symbolSize)));
                        totalWidth += symbolAdvance;
                        //記号の文字を読み飛ばす(最後の1文字はforのi++で読み飛ばす)
                        i += symbolLength - 1;
                        continue;
                    }
                    //この辞書検索が負荷の大部分を占めているのでテーブルルックアップする
                    //ushort glyphIndex = itemFont.GlyphType.CharacterToGlyphMap[text[i]];
                    //double width = itemFont.GlyphType.AdvanceWidths[glyphIndex] * fontSize;
                    ushort glyphIndex = itemFont.NoCache ? (ushort)0 : itemFont.GlyphIndexCache[text[i]];
                    double glyphWidth;
                    GlyphTypeface glyphType = itemFont.GlyphType;
                    if (glyphIndex == 0)
                    {
                        //NoCacheまたはキャッシュミス
                        int key = 0;
                        if (char.IsSurrogatePair(text, i))
                        {
                            key = char.ConvertToUtf32(text, i++);
                        }
                        else if (char.IsSurrogate(text[i]) == false)
                        {
                            key = text[i];
                        }
                        if (glyphType.CharacterToGlyphMap.TryGetValue(key, out glyphIndex) || itemFont.FallbackGlyphType == null)
                        {
                            glyphType.AdvanceWidths.TryGetValue(glyphIndex, out glyphWidth);
                            if (itemFont.NoCache == false && key < itemFont.GlyphIndexCache.Length)
                            {
                                itemFont.GlyphIndexCache[key] = glyphIndex;
                                itemFont.GlyphWidthCache[glyphIndex] = (float)glyphWidth;
                            }
                        }
                        else
                        {
                            glyphType = itemFont.FallbackGlyphType;
                            glyphType.CharacterToGlyphMap.TryGetValue(key, out glyphIndex);
                            glyphType.AdvanceWidths.TryGetValue(glyphIndex, out glyphWidth);
                        }
                        glyphWidth = (float)glyphWidth;
                    }
                    else
                    {
                        glyphWidth = itemFont.GlyphWidthCache[glyphIndex];
                    }
                    double width = glyphWidth * fontSize;

                    if (totalWidth + width > maxWidth)
                    {
                        if (glyphIndexes.Count > 0)
                        {
                            textDrawList.Add(new Tuple<Brush, GlyphRun>(fontColor, CreateGlyphRun(
                                x + originWidth, y + totalHeight, m, selfLeft, currentGlyphType, fontSize, glyphIndexes, advanceWidths)));
                            glyphIndexes = new List<ushort>();
                            advanceWidths = new List<double>();
                        }
                        if (totalHeight > maxHeight)
                        {
                            //次の行無理
                            return false;
                        }
                        totalHeight += fontSize * 1.2;
                        originWidth = totalWidth = 0;
                    }
                    if (glyphType != currentGlyphType)
                    {
                        //フォントが変わった
                        if (glyphIndexes.Count > 0)
                        {
                            textDrawList.Add(new Tuple<Brush, GlyphRun>(fontColor, CreateGlyphRun(
                                x + originWidth, y + totalHeight, m, selfLeft, currentGlyphType, fontSize, glyphIndexes, advanceWidths)));
                            glyphIndexes = new List<ushort>();
                            advanceWidths = new List<double>();
                        }
                        currentGlyphType = glyphType;
                        originWidth = totalWidth;
                    }
                    glyphIndexes.Add(glyphIndex);
                    advanceWidths.Add(width);
                    totalWidth += width;
                }
                if (glyphIndexes.Count > 0)
                {
                    textDrawList.Add(new Tuple<Brush, GlyphRun>(fontColor, CreateGlyphRun(
                        x + originWidth, y + totalHeight, m, selfLeft, currentGlyphType, fontSize, glyphIndexes, advanceWidths)));
                }
                i = text.IndexOf('\n', i);
                i = i < 0 ? text.Length : i + 1;
            }
            return true;
        }

        private static readonly Dictionary<Color, Brush[]> glowBrushCache = new Dictionary<Color, Brush[]>();

        /// <summary>
        /// 文字色に対するぼかしの色(内側用と外側用)を返す。黒系の文字は20%グレー、白系の文字は黒。SolidColorBrush以外はnull
        /// </summary>
        private static Brush[] GetGlowBrushes(Brush textBrush)
        {
            var solid = textBrush as SolidColorBrush;
            if (solid == null)
            {
                return null;
            }
            Brush[] brushes;
            lock (glowBrushCache)
            {
                if (glowBrushCache.TryGetValue(solid.Color, out brushes) == false)
                {
                    Color c = solid.Color;
                    double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
                    //黒系の文字は20%グレー(#CCCCCC、50%グレーは濃すぎたため)、白系の文字は黒でぼかす
                    byte v = luminance < 0.5 ? (byte)204 : (byte)0;
                    brushes = new Brush[] {
                        new SolidColorBrush(Color.FromArgb(128, v, v, v)),
                        new SolidColorBrush(Color.FromArgb(90, v, v, v))
                    };
                    foreach (Brush b in brushes)
                    {
                        b.Freeze();
                    }
                    glowBrushCache[solid.Color] = brushes;
                }
            }
            return brushes;
        }

        /// <summary>
        /// ぼかしを表現するための、文字をずらして重ね描きする位置(内側の輪、外側の輪の順)
        /// </summary>
        private static List<TranslateTransform[]> CreateBlurOffsets(double radius)
        {
            var rings = new List<TranslateTransform[]>();
            foreach (double r in new[] { radius * 0.5, radius })
            {
                double d = r * Math.Sqrt(0.5);
                var ring = new[] {
                    new TranslateTransform(r, 0), new TranslateTransform(-r, 0), new TranslateTransform(0, r), new TranslateTransform(0, -r),
                    new TranslateTransform(d, d), new TranslateTransform(-d, d), new TranslateTransform(d, -d), new TranslateTransform(-d, -d)
                };
                foreach (TranslateTransform t in ring)
                {
                    t.Freeze();
                }
                rings.Add(ring);
            }
            return rings;
        }

        protected override void OnRender(DrawingContext dc)
        {
            PanelLayer layer = Layer;
            bool drawBackground = layer == PanelLayer.All || layer == PanelLayer.Background;
            bool drawSharpText = layer == PanelLayer.All || layer == PanelLayer.Text;
            bool drawGlowLayer = layer == PanelLayer.TitleGlow || layer == PanelLayer.BodyGlow || layer == PanelLayer.BandGlow;
            if (drawBackground)
            {
                dc.DrawRectangle(Background, null, new Rect(RenderSize));
            }
            //重ね描きによるぼかしは、層に分けない(All)ときだけ使う
            bool useRings = layer == PanelLayer.All;
            List<TranslateTransform[]> titleRings = useRings && TitleBlurRadius > 0 && TitleBlurRadius <= 20 ? CreateBlurOffsets(TitleBlurRadius) : null;
            List<TranslateTransform[]> bodyRings = useRings && BodyBlurRadius > 0 && BodyBlurRadius <= 20 ? CreateBlurOffsets(BodyBlurRadius) : null;
            List<TranslateTransform[]> bandRings = useRings && BandMode && BandBlurRadius > 0 && BandBlurRadius <= 20 ? CreateBlurOffsets(BandBlurRadius) : null;

            if (Items != null && textDrawLists != null && Items.Count >= textDrawLists.Count &&
                ContentBrushList != null && ContentBrushList.Count >= 17)
            {
                double selfLeft = Canvas.GetLeft(this);
                Brush bgBrush = Background;
                Brush filteredBrush = null;
                if (bgBrush is SolidColorBrush)
                {
                    Color c = ((SolidColorBrush)bgBrush).Color;
                    filteredBrush = new SolidColorBrush(Color.FromArgb(FilteredAlpha, c.R, c.G, c.B));
                    filteredBrush.Freeze();
                }
                //位置がずれないように枠線の幅が1より大きいときは両側で分け合う
                double borderLeft = _borderLeftSize > 1 ? _borderLeftSize / 2 : _borderLeftSize;
                double borderTop = _borderTopSize > 1 ? _borderTopSize / 2 : _borderTopSize;
                for (int i = 0; i < textDrawLists.Count; i++)
                {
                    ProgramViewItem info = Items[i];
                    double bgHeight = Math.Min(borderTop, info.Height);
                    if (drawBackground && info.Width > _borderLeftSize && bgHeight > 0)
                    {
                        dc.DrawRectangle(bgBrush, null, new Rect(info.LeftPos + borderLeft - selfLeft, info.TopPos, info.Width - _borderLeftSize, bgHeight));
                        dc.DrawRectangle(bgBrush, null, new Rect(info.LeftPos + borderLeft - selfLeft, info.TopPos + info.Height - bgHeight, info.Width - _borderLeftSize, bgHeight));
                    }
                    if (info.Width > _borderLeftSize && info.Height > _borderTopSize)
                    {
                        var textArea = new Rect(info.LeftPos + borderLeft - selfLeft, info.TopPos + borderTop, info.Width - _borderLeftSize, info.Height - _borderTopSize);
                        if (drawBackground)
                        {
                            Brush contentBrush = ContentBrushList[0x10];
                            if (info.EventInfo.ContentInfo != null)
                            {
                                foreach (EpgContentData nibble in info.EventInfo.ContentInfo.nibbleList)
                                {
                                    if (nibble.content_nibble_level_1 <= 0x0B || nibble.content_nibble_level_1 == 0x0F)
                                    {
                                        contentBrush = ContentBrushList[nibble.content_nibble_level_1];
                                        break;
                                    }
                                }
                            }
                            if (BandMode)
                            {
                                //帯の右側はジャンル別背景色を薄くした色、左端の帯はジャンル別背景色(グラデーション表示のときはそれぞれグラデーション)
                                var solidContent = contentBrush as SolidColorBrush;
                                Brush paleBrush = BandGradation && solidContent != null ? GetGradientBrush(GetPaleColor(solidContent.Color)) : GetPaleBrush(contentBrush);
                                Brush bandBrush = BandGradation && solidContent != null ? GetGradientBrush(solidContent.Color) : contentBrush;
                                dc.DrawRectangle(DarkMode ? Brushes.Black : paleBrush, null, textArea);
                                dc.DrawRectangle(bandBrush, null, new Rect(textArea.X, textArea.Y, Math.Min(bandWidthPx, textArea.Width), textArea.Height));
                            }
                            else
                            {
                                dc.DrawRectangle(contentBrush, null, textArea);
                            }
                        }
                        if (layer != PanelLayer.Background)
                        {
                            dc.PushClip(new RectangleGeometry(textArea));
                            foreach (Tuple<Brush, GlyphRun> txtinfo in textDrawLists[i])
                            {
                                if (drawGlowLayer)
                                {
                                    //ぼかしの層: この層の種類の文字を、文字色のまま描く(パネルにかけた影の効果が、文字の形のぼかしを作る。
                                    //サービス名や時間軸と同じ方法)。くっきりした文字は、この層の上のTextの層が重ねて描く
                                    if (RunBelongsToLayer(txtinfo.Item1, layer))
                                    {
                                        dc.DrawGlyphRun(txtinfo.Item1, txtinfo.Item2);
                                    }
                                    continue;
                                }
                                List<TranslateTransform[]> blurRings = ReferenceEquals(txtinfo.Item1, bandTextBrush) ? bandRings :
                                                                       ReferenceEquals(txtinfo.Item1, titleTextBrush) ? titleRings : bodyRings;
                                if (blurRings != null)
                                {
                                    //文字の背景のぼかし(文字を少しずつずらして半透明で重ねる)
                                    Color? ringColor = ResolveGlowColor(txtinfo.Item1);
                                    Brush[] glow = ringColor.HasValue ? GetGlowBrushesByColor(ringColor.Value) : null;
                                    if (glow != null)
                                    {
                                        for (int ring = blurRings.Count - 1; ring >= 0; ring--)
                                        {
                                            foreach (TranslateTransform offset in blurRings[ring])
                                            {
                                                dc.PushTransform(offset);
                                                dc.DrawGlyphRun(glow[ring == blurRings.Count - 1 ? 1 : 0], txtinfo.Item2);
                                                dc.Pop();
                                            }
                                        }
                                    }
                                }
                                if (drawSharpText)
                                {
                                    dc.DrawGlyphRun(txtinfo.Item1, txtinfo.Item2);
                                }
                            }
                            if (drawSharpText && imageDrawLists != null && i < imageDrawLists.Count)
                            {
                                //番組表記号の画像
                                foreach (Tuple<ImageSource, Rect> imageInfo in imageDrawLists[i])
                                {
                                    dc.DrawImage(imageInfo.Item1, imageInfo.Item2);
                                }
                            }
                            dc.Pop();
                            if (drawSharpText && info.Filtered && filteredBrush != null)
                            {
                                dc.DrawRectangle(filteredBrush, null, textArea);
                            }
                        }
                    }
                }
            }
        }
    }
}
