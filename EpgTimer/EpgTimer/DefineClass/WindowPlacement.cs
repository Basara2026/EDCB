using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace EpgTimer
{
    /// <summary>
    /// ウィンドウ位置・サイズの記憶用データ
    /// </summary>
    public class WndPlacementInfo
    {
        public WndPlacementInfo()
        {
            Tag = "";
            Left = double.NaN;
            Top = double.NaN;
            Width = 0;
            Height = 0;
        }
        public string Tag { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        /// <summary>0のときサイズは記憶されていない(内容に合わせて自動調整)</summary>
        public double Width { get; set; }
        public double Height { get; set; }
    }

    /// <summary>
    /// ウィンドウ位置・サイズの復元と保存
    /// </summary>
    static class WindowPlacement
    {
        /// <summary>
        /// 位置・サイズを復元し、閉じるときに保存するようにする。ウィンドウのコンストラクタ(InitializeComponent後)で呼ぶ
        /// </summary>
        public static void Apply(Window win, string tag)
        {
            Restore(win, tag);
            Attach(win, () => tag);
        }

        /// <summary>
        /// 閉じるときに位置・サイズを保存するようにする。tagは保存時点のものが使われる
        /// </summary>
        public static void Attach(Window win, Func<string> getTag)
        {
            Size initialSize = Size.Empty;
            win.ContentRendered += (sender, e) =>
            {
                if (initialSize.IsEmpty)
                {
                    initialSize = new Size(win.ActualWidth, win.ActualHeight);
                }
            };
            win.Closing += (sender, e) =>
            {
                if (e.Cancel == false)
                {
                    Save(win, getTag(), initialSize);
                }
            };
        }

        /// <summary>
        /// 記憶された位置・サイズを復元する。記憶がなければ何もせずfalseを返す
        /// </summary>
        public static bool Restore(Window win, string tag)
        {
            try
            {
                WndPlacementInfo info = Settings.Instance.WndPlacementList.FirstOrDefault(a => a.Tag == tag);
                if (info == null)
                {
                    return false;
                }
                double screenLeft = SystemParameters.VirtualScreenLeft;
                double screenTop = SystemParameters.VirtualScreenTop;
                double screenWidth = SystemParameters.VirtualScreenWidth;
                double screenHeight = SystemParameters.VirtualScreenHeight;

                double width = double.NaN;
                double height = double.NaN;
                if (info.Width > 0 && info.Height > 0)
                {
                    //画面からはみ出さない範囲でサイズを復元する(内容に合わせた自動調整は解除される)
                    width = Math.Min(info.Width, screenWidth);
                    height = Math.Min(info.Height, screenHeight);
                    win.SizeToContent = SizeToContent.Manual;
                    win.Width = width;
                    win.Height = height;
                }
                if (double.IsNaN(info.Left) == false && double.IsNaN(info.Top) == false)
                {
                    //現在の画面構成で見える位置のときだけ復元する
                    double w = double.IsNaN(width) ? (info.Width > 0 ? info.Width : 200) : width;
                    double h = double.IsNaN(height) ? (info.Height > 0 ? info.Height : 100) : height;
                    Rect visible = Rect.Intersect(new Rect(info.Left, info.Top, w, h), new Rect(screenLeft, screenTop, screenWidth, screenHeight));
                    if (visible.IsEmpty == false && info.Top >= screenTop &&
                        visible.Width >= Math.Min(w, 100) && visible.Height >= Math.Min(h, 50))
                    {
                        win.WindowStartupLocation = WindowStartupLocation.Manual;
                        win.Left = info.Left;
                        win.Top = info.Top;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 位置・サイズを保存する。サイズは固定サイズのウィンドウか、利用者が変更したときだけ保存する
        /// </summary>
        public static void Save(Window win, string tag, Size initialSize)
        {
            try
            {
                Rect r = win.WindowState == WindowState.Normal ?
                    new Rect(win.Left, win.Top, win.ActualWidth, win.ActualHeight) : win.RestoreBounds;
                if (r.IsEmpty || double.IsNaN(r.Left) || double.IsNaN(r.Top) || r.Width <= 0 || r.Height <= 0)
                {
                    return;
                }
                bool saveSize = win.SizeToContent == SizeToContent.Manual || win.WindowState != WindowState.Normal ||
                                (initialSize.IsEmpty == false && (Math.Abs(initialSize.Width - r.Width) > 1 || Math.Abs(initialSize.Height - r.Height) > 1));

                List<WndPlacementInfo> list = Settings.Instance.WndPlacementList;
                WndPlacementInfo info = list.FirstOrDefault(a => a.Tag == tag);
                if (info == null)
                {
                    info = new WndPlacementInfo { Tag = tag };
                    list.Add(info);
                }
                info.Left = r.Left;
                info.Top = r.Top;
                if (saveSize)
                {
                    info.Width = r.Width;
                    info.Height = r.Height;
                }
            }
            catch
            {
            }
        }
    }
}
