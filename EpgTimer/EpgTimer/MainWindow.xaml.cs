using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Threading;
using System.Threading; //紅

namespace EpgTimer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window, ITaskTrayClickHandler
    {
        //MainWindowにIDisposableを実装するべき？
        private Mutex mutex;
        private string mutexName;
        private NWConnect nwConnect = new NWConnect();
        private TaskTrayClass taskTray;
        private PipeServer pipeServer = null;
        private bool closeFlag = false;
        private DispatcherTimer loadLogoTimer;

        public MainWindow()
        {
#if NETCOREAPP
            //Encoding.GetEncoding()でCP932を使うため
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            string appName = Path.GetFileNameWithoutExtension(SettingPath.ModuleName);
            CommonManager.Instance.NWMode = appName.StartsWith("EpgTimerNW", StringComparison.OrdinalIgnoreCase);

            Settings.LoadFromXmlFile(CommonManager.Instance.NWMode);

            if (CheckCmdLine() && Settings.Instance.ExitAfterProcessingArgs)
            {
                Environment.Exit(0);
            }
            mutexName = (CommonManager.Instance.NWMode ? "" : "2") +
                        (appName.StartsWith("EpgTimer", StringComparison.OrdinalIgnoreCase) ? appName.Substring(8).ToUpperInvariant() : "");
            mutex = new Mutex(false, "Global\\EpgTimer_Bon" + mutexName);
            if (!mutex.WaitOne(0, false))
            {
                mutex.Close();
                Environment.Exit(0);
            }

#if NETCOREAPP
#pragma warning disable WPF0001
            if (Application.Current.ThemeMode != ThemeMode.None)
            {
                //アプリのResourceDictionaryをFluent用のものに置き換える
                Application.Current.Resources.MergedDictionaries.Remove(
                    Application.Current.Resources.MergedDictionaries.First(a => a.Source != null && a.Source.OriginalString == "/App.rd.xaml"));
                Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/AppFluent.rd.xaml", UriKind.Relative)));
            }
            else
#pragma warning restore WPF0001
#endif
            if (Settings.Instance.NoStyle == 0)
            {
                //アプリのResourceDictionaryをクリア
                Application.Current.Resources.MergedDictionaries.Clear();
                try
                {
                    string path = Path.Combine(SettingPath.ModulePath, SettingPath.ModuleName + ".rd.xaml");
                    if (File.Exists(path))
                    {
                        //ResourceDictionaryを定義したファイルがあるので本体にマージする
                        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Load(System.Xml.XmlReader.Create(path)));
                    }
                    else
                    {
                        //既定のテーマ(Aero)をマージする
                        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
                            new Uri("/PresentationFramework.Aero, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35;component/themes/aero.normalcolor.xaml", UriKind.Relative)));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                //アプリのResourceDictionaryをここでマージ
                Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/App.rd.xaml", UriKind.Relative)));
            }

            if (Settings.Instance.ApplyPostStyle)
            {
                try
                {
                    string path = Path.Combine(SettingPath.ModulePath, SettingPath.ModuleName + ".rdpost.xaml");
                    if (File.Exists(path))
                    {
                        //上書き用のResourceDictionaryをマージ
                        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Load(System.Xml.XmlReader.Create(path)));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }

            var rd = new ResourceDictionary();
            var style = new Style(typeof(Window), (Style)FindResource("AppWindowStyle"));
            double fontSize = (double)FindResource("AppFontSize");
            double largeFontSize = (double)FindResource("AppLargeFontSize");
            if (Settings.Instance.SetAppFont)
            {
                //フォントを上書き
                style.Setters.Add(new Setter(FontFamilyProperty, CommonManager.CreateFontFamily(Settings.Instance.AppFontName)));
                largeFontSize += Settings.Instance.AppFontSize - fontSize;
                fontSize = Settings.Instance.AppFontSize;
                rd.Add("AppFontSize", fontSize);
                rd.Add("AppLargeFontSize", largeFontSize);
            }
            else if (fontSize == 12)
            {
                //既定値のときはシステムのフォントサイズを使う
                largeFontSize += SystemFonts.MessageFontSize - fontSize;
                fontSize = SystemFonts.MessageFontSize;
                rd.Add("AppFontSize", fontSize);
                rd.Add("AppLargeFontSize", largeFontSize);
            }
            style.Setters.Add(new Setter(FontSizeProperty, fontSize));
            rd.Add("AppWindowStyle", style);
            Application.Current.Resources.MergedDictionaries.Add(rd);

            //オリジナルのmutex名をもつEpgTimerか
            if (mutexName == "2")
            {
                try
                {
                    using (Mutex.OpenExisting("Global\\EpgTimer_Bon_Service")) { }
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    //二重起動抑止Mutexが存在しないのでEpgTimerSrvがあれば起動する
                    string exePath = Path.Combine(SettingPath.ModulePath, "EpgTimerSrv.exe");
                    if (File.Exists(exePath))
                    {
                        try
                        {
                            using (Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = false })) { }
                        }
                        catch
                        {
                            MessageBox.Show("EpgTimerSrv.exeの起動ができませんでした");
                            mutex.ReleaseMutex();
                            mutex.Close();
                            Environment.Exit(0);
                        }
                    }
                }
                catch { }
            }

            InitializeComponent();

            //「≡」のメニューが開いている間にボタンを押すと、メニューが閉じたあとのClickで再び開いてしまう。
            //Closedイベントは遅れて届くことがあるので、IsOpenの変化を直接見て、ボタンの押下で閉じたときはClickで開き直さないようにする
            System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ContextMenu.IsOpenProperty, typeof(ContextMenu)).AddValueChanged(moreMenu, (sender, e) =>
            {
                if (moreMenu.IsOpen == false)
                {
                    moreMenuClosedTick = Environment.TickCount;
                    //押している最中にボタン上で閉じたなら、そのClickでは開かない
                    if (Mouse.LeftButton == MouseButtonState.Pressed && IsPointerOnMoreButton())
                    {
                        moreMenuSuppressClick = true;
                    }
                }
            });
            //メニューがマウスを捕まえている間のボタン上の押下は、ボタンではなくここに届く
            Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(moreMenu, (sender, e) =>
            {
                if (moreMenu.IsOpen && IsPointerOnMoreButton())
                {
                    moreMenuSuppressClick = true;
                    moreMenu.IsOpen = false;
                }
            });
            //ボタンに押下が届いたとき、メニューが開いている(または直前に閉じた)なら閉じるだけにして、Clickを起こさない
            button_more.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((sender, e) =>
            {
                if (moreMenu.IsOpen || unchecked(Environment.TickCount - moreMenuClosedTick) < 300)
                {
                    moreMenu.IsOpen = false;
                    e.Handled = true;
                }
            }), true);
            //Clickのあと(handledEventsToo)と、ボタンの外へ出たときに、開き直さないための印を解除する
            button_more.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((sender, e) => moreMenuSuppressClick = false), true);
            button_more.MouseLeave += (sender, e) => moreMenuSuppressClick = false;

            //上部のプルダウンメニューを右クリックしたとき、グレーアウトした「切り取り/コピー/貼り付け」が出ないようにする
            //(プルダウン内部のテキストボックスの既定のメニュー。右クリックを離した処理を先に受けて止め、メニューを開かせない)
            grid_toolbar.PreviewMouseRightButtonUp += (sender, e) => e.Handled = true;
            grid_toolbar.PreviewKeyDown += (sender, e) =>
            {
                //キーボードからのメニュー表示(アプリケーションキー、Shift+F10)も同様
                if (e.Key == Key.Apps || (e.Key == Key.F10 && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None))
                {
                    e.Handled = true;
                }
            };

            Title = appName;

            //スクロールバーの操作性のため
            tabControl_main.BorderThickness = new Thickness(0, tabControl_main.BorderThickness.Top, 0, 0);
            tabControl_main.Padding = new Thickness(0, tabControl_main.Padding.Top, 0, 0);

            //「ボタンレイアウト変更」のプルダウンメニューの行を、タブや番組表の切り替えに合わせて更新する
            tabControl_main.SelectionChanged += (sender, e) =>
            {
                if (ReferenceEquals(e.OriginalSource, tabControl_main))
                {
                    UpdateToolbar();
                }
            };
            epgView.NavigationChanged += (sender, e) => UpdateToolbar();
            autoAddView.NavigationChanged += (sender, e) => UpdateToolbar();

            try
            {
                //ウインドウ位置の復元
                if (Settings.Instance.MainWndTop != -100)
                {
                    this.Top = Settings.Instance.MainWndTop;
                }
                if (Settings.Instance.MainWndLeft != -100)
                {
                    this.Left = Settings.Instance.MainWndLeft;
                }
                if (Settings.Instance.MainWndWidth != -100)
                {
                    this.Width = Settings.Instance.MainWndWidth;
                }
                if (Settings.Instance.MainWndHeight != -100)
                {
                    this.Height = Settings.Instance.MainWndHeight;
                }
                if (Settings.Instance.WakeMin)
                {
                    if (Settings.Instance.ShowTray && Settings.Instance.MinHide)
                    {
                        Visibility = Visibility.Hidden;
                    }
                    WindowState = WindowState.Minimized;
                }
                else
                {
                    WindowState = Settings.Instance.LastWindowState;
                }

                ResetButtonView();

                if (CommonManager.Instance.NWMode == false)
                {
                    int pid;
                    using (var process = Process.GetCurrentProcess())
                    {
                        pid = process.Id;
                    }
                    //コールバックは別スレッドかもしれないので設定は予めキャプチャする
                    uint execBat = Settings.Instance.ExecBat;
                    pipeServer = new PipeServer("Global\\EpgTimerGUI_Ctrl_BonConnect_" + pid,
                                                "EpgTimerGUI_Ctrl_BonPipe_" + pid,
                                                (c, r) => OutsideCmdCallback(c, r, false, execBat));

                    for (int i = 0; i < 150; i++)
                    {
                        if (CommonManager.CreateSrvCtrl().SendRegistGUI((uint)pid) == ErrCode.CMD_SUCCESS)
                        {
                            CommonManager.Instance.DB.ReloadChSet5();
                            break;
                        }
                        Thread.Sleep(100);
                    }
                }

                //タスクトレイの表示
                taskTray = new TaskTrayClass(this);
                taskTray.IconUri = new Uri("pack://application:,,,/Resources/TaskIconBlue.ico");
                taskTray.ForceHideBalloonTipSec = Settings.Instance.ForceHideBalloonTipSec;
                Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                       new Action(() => taskTray.Visible = Settings.Instance.ShowTray));

                CommonManager.Instance.DB.ChSet5LogoChanged += epgView.RefreshLogo;
                CommonManager.Instance.DB.EpgDataChanged += reserveView.RefreshEpgData;
                CommonManager.Instance.DB.ReserveInfoChanged += epgView.RefreshReserve;
                CommonManager.Instance.DB.ReserveInfoChanged += reserveView.Refresh;
                CommonManager.Instance.DB.ReserveInfoChanged += tunerReserveView.Refresh;
                if (Settings.Instance.NgAutoEpgLoadNW == false)
                {
                    //EPGデータを遅延更新しない
                    CommonManager.Instance.DB.ReloadEpgData();
                }
                //予約情報は常に遅延更新しない
                CommonManager.Instance.DB.ReloadReserveInfo();

                loadLogoTimer = new DispatcherTimer();
                loadLogoTimer.Interval = TimeSpan.FromMilliseconds(500);
                loadLogoTimer.Tick += (sender, e) =>
                {
                    if (CommonManager.Instance.DB.LoadChSet5Logo())
                    {
                        loadLogoTimer.Stop();
                    }
                };
                loadLogoTimer.Start();

                //初期タブ選択
                switch (Settings.Instance.StartTab)
                {
                    //case "ReserveView":
                    //    this.tabItem_reserve.IsSelected = true;
                    //    break;
                    case "TunerReserveView":
                        this.tabItem_tunerReserve.IsSelected = true;
                        break;
                    case "RecInfoView":
                        this.tabItem_recinfo.IsSelected = true;
                        break;
                    case "EpgAutoAddView":
                        this.tabItem_epgAutoAdd.IsSelected = true;
                    //    this.autoAddView.tabItem_epg.IsSelected = true;
                        break;
                    case "ManualAutoAddView":
                        this.tabItem_epgAutoAdd.IsSelected = true;
                        this.autoAddView.tabItem_manual.IsSelected = true;
                        break;
                    case "EpgView":
                        this.tabItem_epg.IsSelected = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private static bool CheckCmdLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length <= 1)
            {
                //引数なし
                return false;
            }
            var cmd = new CtrlCmdUtil();
            if (CommonManager.Instance.NWMode)
            {
                cmd.SetSendMode(true);
                bool connected = false;
                try
                {
                    //IPv4の名前解決を優先する
                    foreach (var address in System.Net.Dns.GetHostAddresses(Settings.Instance.NWServerIP).OrderBy(a => a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
                    {
                        cmd.SetNWSetting(address, Settings.Instance.NWServerPort);
                        byte[] binData;
                        if (cmd.SendFileCopy("ChSet5.txt", out binData) == ErrCode.CMD_SUCCESS)
                        {
                            connected = ChSet5.LoadWithStreamReader(new MemoryStream(binData));
                            break;
                        }
                    }
                }
                catch { }
                if (connected == false)
                {
                    MessageBox.Show("EpgTimerSrvとの接続に失敗しました。EpgTimerNWの接続設定を確認してください。");
                    return true;
                }
            }
            else
            {
                byte[] binData;
                if (cmd.SendFileCopy("ChSet5.txt", out binData) != ErrCode.CMD_SUCCESS ||
                    ChSet5.LoadWithStreamReader(new MemoryStream(binData)) == false)
                {
                    MessageBox.Show("EpgTimerSrvとの接続に失敗しました。");
                    return true;
                }
            }
            SendAddReserveFromArgs(cmd, args.Skip(1));
            return true;
        }

        private void ResetButtonView()
        {
            for (int i = 0; i < stackPanel_button.Children.Count; i++)
            {
                if (stackPanel_button.Children[i] is Button)
                {
                    stackPanel_button.Children[i].Visibility = Visibility.Collapsed;
                }
                else
                {
                    stackPanel_button.Children.RemoveAt(i--);
                }
            }
            for (int i = 0; i < tabControl_main.Items.Count; i++)
            {
                TabItem ti = tabControl_main.Items.GetItemAt(i) as TabItem;
                if (ti != null && ti.Tag is string && ((string)ti.Tag).StartsWith("PushLike", StringComparison.Ordinal))
                {
                    tabControl_main.Items.Remove(ti);
                    i--;
                }
            }
            foreach (string info in Settings.Instance.ViewButtonList)
            {
                if (info == "（空白）")
                {
                    var separator = new FrameworkElement() { Width = (double)FindResource("AppMainWindowButtonSeparatorWidth") };
                    stackPanel_button.Children.Add(separator);
                }
                else
                {
                    var button = stackPanel_button.Children.OfType<Button>().FirstOrDefault(a => (string)(a.Tag ?? a.Content) == info);
                    if (button != null)
                    {
                        if (info == "カスタム１")
                        {
                            button.Content = Settings.Instance.Cust1BtnName;
                        }
                        if (info == "カスタム２")
                        {
                            button.Content = Settings.Instance.Cust2BtnName;
                        }

                        //必要なボタンだけ可視化
                        stackPanel_button.Children.Remove(button);
                        stackPanel_button.Children.Add(button);
                        button.Visibility = Visibility.Visible;
                    }
                }
            }
            ApplyViewFonts();
            ApplyTabLayout();
        }

        /// <summary>
        /// 予約一覧、録画済み一覧、自動予約登録、使用予定チューナーの各画面と、上部のプルダウンメニューのフォントとサイズを、動作設定の値にする。
        /// 空欄・0の項目は、全体のフォントの設定(なければ既定)を使う
        /// </summary>
        private void ApplyViewFonts()
        {
            Settings settings = Settings.Instance;
            ApplyViewFont(reserveView, settings.ReserveViewFontName, settings.ReserveViewFontSize, settings.ReserveViewFontWeight);
            ApplyViewFont(recInfoView, settings.RecInfoViewFontName, settings.RecInfoViewFontSize, settings.RecInfoViewFontWeight);
            ApplyViewFont(autoAddView, settings.AutoAddViewFontName, settings.AutoAddViewFontSize, settings.AutoAddViewFontWeight);
            ApplyViewFont(tunerReserveView, settings.TunerReserveViewFontName, settings.TunerReserveViewFontSize, settings.TunerReserveViewFontWeight);
            ApplyViewFont(grid_toolbar, settings.ToolbarFontName, settings.ToolbarFontSize, settings.ToolbarFontWeight);
            ApplyViewFont(moreMenu, settings.ToolbarFontName, settings.ToolbarFontSize, settings.ToolbarFontWeight);
        }

        private static void ApplyViewFont(DependencyObject element, string fontName, double fontSize, int fontWeight)
        {
            if (string.IsNullOrWhiteSpace(fontName))
            {
                element.ClearValue(TextElement.FontFamilyProperty);
            }
            else
            {
                element.SetValue(TextElement.FontFamilyProperty, CommonManager.CreateFontFamily(fontName));
            }
            if (fontSize >= 1 && fontSize <= 200)
            {
                element.SetValue(TextElement.FontSizeProperty, fontSize);
            }
            else
            {
                element.ClearValue(TextElement.FontSizeProperty);
            }
            if (fontWeight >= 1 && fontWeight <= 999 && fontWeight != 400)
            {
                element.SetValue(TextElement.FontWeightProperty, FontWeight.FromOpenTypeWeight(fontWeight));
            }
            else
            {
                element.ClearValue(TextElement.FontWeightProperty);
            }
        }

        private ContextMenu moreMenu = new ContextMenu();
        private int moreMenuClosedTick = Environment.TickCount - 100000;
        private bool moreMenuSuppressClick = false;

        private bool IsPointerOnMoreButton()
        {
            //メニューがマウスを捕まえている間はIsMouseOverが当てにならないので、座標で判定する
            Point pos = Mouse.GetPosition(button_more);
            return pos.X >= 0 && pos.Y >= 0 && pos.X < button_more.ActualWidth && pos.Y < button_more.ActualHeight;
        }
        private bool updatingToolbar = false;
        private TabItem[] mainTabOrder;

        /// <summary>
        /// 「ボタンレイアウト変更」のとき、ボタンやタブの見出しの代わりに、ウィンドウ上部にプルダウンメニューの行を表示する。
        /// 左から、表示の切り替え、番組表の切り替え(自動予約登録のときはEPG予約/プログラム予約の切り替え)、日付・時間帯の切り替え。右端の「≡」には設定などのボタンをまとめる
        /// </summary>
        private void ApplyTabLayout()
        {
            bool layout = Settings.Instance.ViewButtonShowAsTab;
            grid_toolbar.Visibility = layout ? Visibility.Visible : Visibility.Collapsed;
            stackPanel_button.Visibility = layout ? Visibility.Collapsed : Visibility.Visible;
            TabHeaderLayout.SetHeaderVisible(tabControl_main, layout == false);
            epgView.SetHeaderVisible(layout == false);
            autoAddView.SetHeaderVisible(layout == false);
            if (layout)
            {
                BuildMoreMenu();
                if (mainTabOrder == null)
                {
                    mainTabOrder = new[] { tabItem_epg, tabItem_reserve, tabItem_recinfo, tabItem_epgAutoAdd, tabItem_tunerReserve };
                    updatingToolbar = true;
                    foreach (TabItem tab in mainTabOrder)
                    {
                        comboBox_main.Items.Add(tab.Header);
                    }
                    updatingToolbar = false;
                }
            }
            UpdateToolbar();
        }

        /// <summary>
        /// 右端の「≡」のメニューを、表示するボタンの設定に合わせて作る
        /// </summary>
        private void BuildMoreMenu()
        {
            moreMenu.Items.Clear();
            foreach (UIElement child in stackPanel_button.Children)
            {
                var button = child as Button;
                if (button != null)
                {
                    if (button.Visibility == Visibility.Visible)
                    {
                        var target = button;
                        var item = new MenuItem();
                        item.Header = button.Content as string ?? button.Tag as string;
                        item.Click += (sender, e) => target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        moreMenu.Items.Add(item);
                    }
                }
                else if (child is FrameworkElement && moreMenu.Items.Count > 0 && (moreMenu.Items[moreMenu.Items.Count - 1] is Separator) == false)
                {
                    //「（空白）」の項目は区切り線にする
                    moreMenu.Items.Add(new Separator());
                }
            }
            button_more.IsEnabled = moreMenu.Items.Count > 0;
        }

        /// <summary>
        /// プルダウンメニューの行の選択状態を、現在のタブと番組表に合わせる
        /// </summary>
        private void UpdateToolbar()
        {
            if (Settings.Instance.ViewButtonShowAsTab == false || mainTabOrder == null)
            {
                return;
            }
            updatingToolbar = true;
            try
            {
                comboBox_main.SelectedIndex = Array.IndexOf(mainTabOrder, tabControl_main.SelectedItem as TabItem);

                //番組表のタブが選ばれているときだけ、番組表の切り替えと日付・時間帯のメニューを表示する
                bool epgSelected = ReferenceEquals(tabControl_main.SelectedItem, tabItem_epg);
                List<string> names = epgView.GetTabNames();
                if (comboBox_epgTab.Items.Count != names.Count || names.Where((name, i) => (string)comboBox_epgTab.Items[i] != name).Any())
                {
                    comboBox_epgTab.Items.Clear();
                    foreach (string name in names)
                    {
                        comboBox_epgTab.Items.Add(name);
                    }
                }
                comboBox_epgTab.SelectedIndex = epgView.SelectedTabIndex;
                comboBox_epgTab.Visibility = epgSelected && names.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                FrameworkElement datePulldown = epgView.GetSelectedDatePulldown();
                if (ReferenceEquals(contentControl_date.Content, datePulldown) == false)
                {
                    contentControl_date.Content = datePulldown;
                }
                contentControl_date.Visibility = epgSelected && datePulldown != null ? Visibility.Visible : Visibility.Collapsed;

                //自動予約登録のタブが選ばれているときだけ、EPG予約/プログラム予約の切り替えを表示する
                bool autoAddSelected = ReferenceEquals(tabControl_main.SelectedItem, tabItem_epgAutoAdd);
                List<string> autoAddNames = autoAddView.GetTabNames();
                if (comboBox_autoAddTab.Items.Count != autoAddNames.Count || autoAddNames.Where((name, i) => (string)comboBox_autoAddTab.Items[i] != name).Any())
                {
                    comboBox_autoAddTab.Items.Clear();
                    foreach (string name in autoAddNames)
                    {
                        comboBox_autoAddTab.Items.Add(name);
                    }
                }
                comboBox_autoAddTab.SelectedIndex = autoAddView.SelectedTabIndex;
                comboBox_autoAddTab.Visibility = autoAddSelected && autoAddNames.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                updatingToolbar = false;
            }
        }

        private void comboBox_main_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingToolbar == false && mainTabOrder != null && comboBox_main.SelectedIndex >= 0)
            {
                tabControl_main.SelectedItem = mainTabOrder[comboBox_main.SelectedIndex];
            }
        }

        private void comboBox_epgTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingToolbar == false && comboBox_epgTab.SelectedIndex >= 0)
            {
                epgView.SelectedTabIndex = comboBox_epgTab.SelectedIndex;
            }
        }

        private void comboBox_autoAddTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingToolbar == false && comboBox_autoAddTab.SelectedIndex >= 0)
            {
                autoAddView.SelectedTabIndex = comboBox_autoAddTab.SelectedIndex;
            }
        }

        private void moreButton_Click(object sender, RoutedEventArgs e)
        {
            if (moreMenuSuppressClick)
            {
                //開いていたメニューをこのボタンの押下で閉じたところなので、開き直さない
                moreMenuSuppressClick = false;
                return;
            }
            if (moreMenu.IsOpen)
            {
                moreMenu.IsOpen = false;
                return;
            }
            //メニューの右端を「≡」ボタンの右端にそろえて、ウィンドウの内側に表示する(右側に空きがあっても外側にはみ出さない)
            moreMenu.PlacementTarget = button_more;
            moreMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Custom;
            moreMenu.CustomPopupPlacementCallback = (popupSize, targetSize, offset) => new[]
            {
                new System.Windows.Controls.Primitives.CustomPopupPlacement(
                    new Point(targetSize.Width - popupSize.Width, targetSize.Height),
                    System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)
            };
            moreMenu.IsOpen = true;
        }

        public void TaskTrayLeftClick()
        {
            Show();
            WindowState = Settings.Instance.LastWindowState;
            Activate();
        }

        public void TaskTrayRightClick()
        {
            var menu = new ContextMenu();
            if (Settings.ContextMenuResourceDictionary != null)
            {
                menu.Resources.MergedDictionaries.Add(Settings.ContextMenuResourceDictionary);
            }
            foreach (string info in Settings.Instance.TaskMenuList)
            {
                if (info == "（セパレータ）")
                {
                    menu.Items.Add(new Separator());
                    continue;
                }
                var item = new MenuItem();
                if (info == "設定")
                {
                    item.Click += (sender, e) =>
                    {
                        //メインウィンドウが表示済みであることを前提にしている箇所があるため
                        if (PresentationSource.FromVisual(this) == null)
                        {
                            Show();
                            WindowState = Settings.Instance.LastWindowState;
                        }
                        SettingCmd();
                    };
                }
                else if (info == "終了")
                {
                    item.Click += (sender, e) => CloseCmd();
                }
                else if (info == "スタンバイ")
                {
                    item.Click += (sender, e) => StandbyCmd();
                }
                else if (info == "休止")
                {
                    item.Click += (sender, e) => SuspendCmd();
                }
                else if (info == "EPG取得")
                {
                    item.Click += (sender, e) => EpgCapCmd();
                }
                else if (info == "再接続" && CommonManager.Instance.NWMode)
                {
                    item.Click += (sender, e) => ConnectCmd(true);
                }
                else
                {
                    continue;
                }
                item.Header = info;
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
            var ps = PresentationSource.FromVisual(menu);
            if (ps != null)
            {
                //Activate()したいがContextMenuからWindowを取得できないので仕方なく
                CommonUtil.SetForegroundWindow(((System.Windows.Interop.HwndSource)ps).Handle);
                menu.Focus();
            }
        }

        bool ConnectCmd(bool showDialog)
        {
            if (showDialog == true)
            {
                ConnectWindow dlg = new ConnectWindow();
                PresentationSource topWindow = PresentationSource.FromVisual(this);
                if (topWindow != null)
                {
                    dlg.Owner = (Window)topWindow.RootVisual;
                }
                if (dlg.ShowDialog() == false)
                {
                    return true;
                }
            }

            bool connected = false;
            try
            {
                //IPv4の名前解決を優先する
                foreach (var address in System.Net.Dns.GetHostAddresses(Settings.Instance.NWServerIP).OrderBy(a => a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
                {
                    //コールバックは別スレッドかもしれないので設定は予めキャプチャする
                    uint execBat = Settings.Instance.ExecBat;
                    CommonManager.Instance.NWConnectedIP = null;
                    if (nwConnect.ConnectServer(address, Settings.Instance.NWServerPort, Settings.Instance.NWWaitPort, (c, r) => OutsideCmdCallback(c, r, true, execBat)))
                    {
                        CommonManager.Instance.NWConnectedIP = address;
                        CommonManager.Instance.NWConnectedPort = Settings.Instance.NWServerPort;
                        connected = true;
                        break;
                    }
                }
            }
            catch
            {
            }
            if (connected == false)
            {
                if (showDialog == true)
                {
                    MessageBox.Show("サーバーへの接続に失敗しました");
                }
                return false;
            }

            CommonManager.Instance.DB.ReloadChSet5();
            CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.ReserveInfo);
            CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.RecInfo);
            CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.AutoAddEpgInfo);
            CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.AutoAddManualInfo);
            CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.EpgData);
            if (Settings.Instance.NgAutoEpgLoadNW == false)
            {
                //EPGデータを遅延更新しない
                CommonManager.Instance.DB.ReloadEpgData();
            }
            //予約情報は常に遅延更新しない
            CommonManager.Instance.DB.ReloadReserveInfo();
            autoAddView.UpdateAutoAddInfo();
            recInfoView.UpdateInfo();
            epgView.UpdateEpgData();
            loadLogoTimer.Start();
            return true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (CommonManager.Instance.NWMode == true)
            {
                if (Settings.Instance.WakeReconnectNW == false || ConnectCmd(false) == false)
                {
                    ConnectCmd(true);
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Settings.Instance.CloseMin == true && closeFlag == false)
            {
                e.Cancel = true;
                WindowState = System.Windows.WindowState.Minimized;
            }
            else
            {
                loadLogoTimer.Stop();
                reserveView.SaveSize();
                recInfoView.SaveSize();
                autoAddView.SaveSize();
                if (Visibility == Visibility.Visible)
                {
                    Rect r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
                    Settings.Instance.MainWndLeft = Math.Max(r.Left, -50);
                    Settings.Instance.MainWndTop = Math.Max(r.Top, 0);
                    Settings.Instance.MainWndWidth = Math.Max(r.Width, 51);
                    Settings.Instance.MainWndHeight = Math.Max(r.Height, 1);
                }
                Settings.SaveToXmlFile();

                if (CommonManager.Instance.NWMode == false)
                {
                    var cmd = CommonManager.CreateSrvCtrl();
                    cmd.SetConnectTimeOut(3000);
                    using (var process = Process.GetCurrentProcess())
                    {
                        cmd.SendUnRegistGUI((uint)process.Id);
                    }
                    //オリジナルのmutex名をもつEpgTimerか
                    if (mutexName == "2")
                    {
                        //実際にEpgTimerSrvを終了するかどうかは(現在は)EpgTimerSrvの判断で決まる
                        //このフラグはEpgTimerと原作のサービスモードのEpgTimerSrvを混用するなど特殊な状況を想定したもの
                        if (Settings.Instance.NoSendClose == 0)
                        {
                            cmd.SendClose();
                        }
                    }
                    pipeServer.Dispose();
                }
                else
                {
                    if (CommonManager.Instance.NWConnectedIP != null && Settings.Instance.NWWaitPort != 0)
                    {
                        CommonManager.CreateSrvCtrl().SendUnRegistTCP(Settings.Instance.NWWaitPort);
                    }
                    nwConnect.Dispose();
                }
                mutex.ReleaseMutex();
                mutex.Close();
                taskTray.Dispose();
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                if (Settings.Instance.ShowTray && Settings.Instance.MinHide)
                {
                    if (Visibility == Visibility.Visible)
                    {
                        //スナップ機能(半分最大化など)はWindowState.NormalだがRestoreBoundsはMaximize的に変化するため少し不正確
                        Settings.Instance.MainWndLeft = Math.Max(RestoreBounds.Left, -50);
                        Settings.Instance.MainWndTop = Math.Max(RestoreBounds.Top, 0);
                        Settings.Instance.MainWndWidth = Math.Max(RestoreBounds.Width, 51);
                        Settings.Instance.MainWndHeight = Math.Max(RestoreBounds.Height, 1);
                        Visibility = Visibility.Hidden;
                    }
                }
            }
            else
            {
                Settings.Instance.LastWindowState = this.WindowState;
            }
        }

        private void Window_PreviewDragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            string[] filePath = e.Data.GetData(DataFormats.FileDrop, true) as string[];
            if (filePath != null) SendAddReserveFromArgs(CommonManager.CreateSrvCtrl(), filePath);
        }

        private static void SendAddReserveFromArgs(CtrlCmdUtil cmd, IEnumerable<string> args)
        {
            var addList = new List<ReserveData>();
            foreach (string arg in args)
            {
                ReserveData info = null;
                if (arg.EndsWith(".tvpid", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".tvpio", StringComparison.OrdinalIgnoreCase))
                {
                    //iEPG追加
                    info = IEPGFileClass.TryLoadTVPID(arg, ChSet5.Instance.ChList);
                    if (info == null)
                    {
                        MessageBox.Show("解析に失敗しました。デジタル用Version 2のiEPGの必要があります。");
                        return;
                    }
                }
                else if (arg.EndsWith(".tvpi", StringComparison.OrdinalIgnoreCase))
                {
                    //iEPG追加
                    info = IEPGFileClass.TryLoadTVPI(arg, ChSet5.Instance.ChList, Settings.Instance.IEpgStationList);
                    if (info == null)
                    {
                        MessageBox.Show("解析に失敗しました。放送局名がサービスに関連づけされていない可能性があります。");
                        return;
                    }
                }
                if (info != null)
                {
                    ulong pgID = CommonManager.Create64PgKey(info.OriginalNetworkID, info.TransportStreamID, info.ServiceID, info.EventID);
                    var pgInfo = new EpgEventInfo();
                    if (info.EventID != 0xFFFF && cmd.SendGetPgInfo(pgID, ref pgInfo) == ErrCode.CMD_SUCCESS)
                    {
                        //番組情報が見つかったので更新しておく
                        if (pgInfo.ShortInfo != null)
                        {
                            info.Title = pgInfo.ShortInfo.event_name;
                        }
                        if (pgInfo.StartTimeFlag != 0)
                        {
                            info.StartTime = pgInfo.start_time;
                            info.StartTimeEpg = pgInfo.start_time;
                        }
                        if (pgInfo.DurationFlag != 0)
                        {
                            info.DurationSecond = pgInfo.durationSec;
                        }
                    }
                    info.RecSetting = Settings.CreateRecSetting(0);
                    addList.Add(info);
                }
            }
            if (addList.Count > 0)
            {
                var list = new List<ReserveData>();
                if (cmd.SendEnumReserve(ref list) == ErrCode.CMD_SUCCESS)
                {
                    //重複除去
                    addList = addList.Where(a => list.All(b =>
                        a.OriginalNetworkID != b.OriginalNetworkID ||
                        a.TransportStreamID != b.TransportStreamID ||
                        a.ServiceID != b.ServiceID ||
                        a.EventID != b.EventID ||
                        a.EventID == 0xFFFF && (a.StartTime != b.StartTime || a.DurationSecond != b.DurationSecond))).ToList();
                    if (addList.Count == 0 || cmd.SendAddReserve(addList) == ErrCode.CMD_SUCCESS)
                    {
                        return;
                    }
                }
                MessageBox.Show("予約追加に失敗しました。");
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.F:
                        if (e.IsRepeat == false)
                        {
                            var button = stackPanel_button.Children.OfType<Button>().First(a => (string)(a.Tag ?? a.Content) == "検索");
                            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        e.Handled = true;
                        break;
                    case Key.D1:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_reserve.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                    case Key.D2:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_tunerReserve.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                    case Key.D3:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_recinfo.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                    case Key.D4:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_epgAutoAdd.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                    case Key.D5:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_epg.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                }
            }
        }

        void settingButton_Click(object sender, RoutedEventArgs e)
        {
            SettingCmd();
        }

        void SettingCmd()
        {
            PresentationSource topWindow = PresentationSource.FromVisual(this);
            if (topWindow != null && OwnedWindows.OfType<SettingWindow>().FirstOrDefault() == null)
            {
                var setting = new SettingWindow();
                setting.Owner = (Window)topWindow.RootVisual;
                //「適用」ボタンでも、OKで閉じたときと同じ反映を行う
                setting.SettingApplied += (sender, e) => ApplySettingChanges();
                if (setting.ShowDialog() == true)
                {
                    ApplySettingChanges();
                }
            }
        }

        /// <summary>
        /// 設定ウィンドウで変更した設定を画面に反映する
        /// </summary>
        void ApplySettingChanges()
        {
            epgView.UpdateSetting();
            tunerReserveView.Refresh();
            loadLogoTimer.Start();
            ResetButtonView();
            taskTray.ForceHideBalloonTipSec = Settings.Instance.ForceHideBalloonTipSec;
            taskTray.Visible = Settings.Instance.ShowTray;
        }

        void searchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchWindow search = OwnedWindows.OfType<SearchWindow>().FirstOrDefault();
            if (search != null)
            {
                //既存のウィンドウをアクティブにする
                if (search.WindowState == WindowState.Minimized)
                {
                    search.WindowState = WindowState.Normal;
                }
                search.Activate();
            }
            else
            {
                CreateSearchWindow().Show();
            }
        }

        void closeButton_Click(object sender, RoutedEventArgs e)
        {
            CloseCmd();
        }

        void CloseCmd()
        {
            closeFlag = true;
            Close();
        }

        void epgCapButton_Click(object sender, RoutedEventArgs e)
        {
            EpgCapCmd();
        }

        void EpgCapCmd()
        {
            if (CommonManager.CreateSrvCtrl().SendEpgCapNow() != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show("EPG取得を行える状態ではありません。\r\n（もうすぐ予約が始まる。EPGデータ読み込み中。など）");
            }
        }

        void epgReloadButton_Click(object sender, RoutedEventArgs e)
        {
            EpgReloadCmd();
        }

        void EpgReloadCmd()
        {
            if (CommonManager.CreateSrvCtrl().SendReloadEpg() != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show("EPG再読み込みを行える状態ではありません。\r\n（EPGデータ読み込み中。など）");
            }
        }

        void suspendButton_Click(object sender, RoutedEventArgs e)
        {
            SuspendCmd();
        }

        void SuspendCmd()
        {
            ErrCode err = CommonManager.CreateSrvCtrl().SendChkSuspend();
            if (err != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show(CommonManager.GetErrCodeText(err) ?? "休止に移行できる状態ではありません。\r\n（もうすぐ予約が始まる。または抑制条件のexeが起動している。など）");
            }
            else
            {
                if (CommonManager.Instance.NWMode == false)
                {
                    if (Settings.Instance.SuspendChk == 1)
                    {
                        SuspendCheckWindow dlg = new SuspendCheckWindow();
                        dlg.SetMode(0, 2);
                        if (dlg.ShowDialog() != true)
                        {
                            return;
                        }
                    }
                    CommonManager.CreateSrvCtrl().SendSuspend(0xFF02);
                }
                else
                {
                    if (Settings.Instance.SuspendCloseNW == true)
                    {
                        if (CommonManager.Instance.NWConnectedIP != null)
                        {
                            if (Settings.Instance.NWWaitPort != 0)
                            {
                                CommonManager.CreateSrvCtrl().SendUnRegistTCP(Settings.Instance.NWWaitPort);
                            }
                            CommonManager.CreateSrvCtrl().SendSuspend(0xFF02);
                            CommonManager.Instance.NWConnectedIP = null;
                            CloseCmd();
                        }
                    }
                    else
                    {
                        CommonManager.CreateSrvCtrl().SendSuspend(0xFF02);
                    }
                }
            }
        }

        void standbyButton_Click(object sender, RoutedEventArgs e)
        {
            StandbyCmd();
        }

        void StandbyCmd()
        {
            ErrCode err = CommonManager.CreateSrvCtrl().SendChkSuspend();
            if (err != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show(CommonManager.GetErrCodeText(err) ?? "スタンバイに移行できる状態ではありません。\r\n（もうすぐ予約が始まる。または抑制条件のexeが起動している。など）");
            }
            else
            {
                if (CommonManager.Instance.NWMode == false)
                {
                    if (Settings.Instance.SuspendChk == 1)
                    {
                        SuspendCheckWindow dlg = new SuspendCheckWindow();
                        dlg.SetMode(0, 1);
                        if (dlg.ShowDialog() != true)
                        {
                            return;
                        }
                    }
                    CommonManager.CreateSrvCtrl().SendSuspend(0xFF01);
                }
                else
                {
                    if (Settings.Instance.SuspendCloseNW == true)
                    {
                        if (CommonManager.Instance.NWConnectedIP != null)
                        {
                            if (Settings.Instance.NWWaitPort != 0)
                            {
                                CommonManager.CreateSrvCtrl().SendUnRegistTCP(Settings.Instance.NWWaitPort);
                            }
                            CommonManager.CreateSrvCtrl().SendSuspend(0xFF01);
                            CommonManager.Instance.NWConnectedIP = null;
                            CloseCmd();
                        }
                    }
                    else
                    {
                        CommonManager.CreateSrvCtrl().SendSuspend(0xFF01);
                    }
                }
            }
        }

        void custum1Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (Process.Start(new ProcessStartInfo(Settings.Instance.Cust1BtnCmd, Settings.Instance.Cust1BtnCmdOpt) { UseShellExecute = true })) { }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        void custum2Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (Process.Start(new ProcessStartInfo(Settings.Instance.Cust2BtnCmd, Settings.Instance.Cust2BtnCmdOpt) { UseShellExecute = true })) { }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        void nwTVEndButton_Click(object sender, RoutedEventArgs e)
        {
            CommonManager.Instance.TVTestCtrl.CloseTVTest();
        }

        void logViewButton_Click(object sender, RoutedEventArgs e)
        {
            NotifyLogWindow dlg = new NotifyLogWindow();
            PresentationSource topWindow = PresentationSource.FromVisual(this);
            if (topWindow != null)
            {
                dlg.Owner = (Window)topWindow.RootVisual;
            }
            dlg.ShowDialog();
        }

        void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if (CommonManager.Instance.NWMode == true)
            {
                ConnectCmd(true);
            }
        }

        private Tuple<ErrCode, byte[], uint> OutsideCmdCallback(uint cmdParam, byte[] cmdData, bool networkFlag, uint execBat)
        {
            Trace.WriteLine((CtrlCmd)cmdParam);
            var res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_NON_SUPPORT, null, 0);

            switch ((CtrlCmd)cmdParam)
            {
                case CtrlCmd.CMD_TIMER_GUI_SHOW_DLG:
                    if (networkFlag == false)
                    {
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, null, 0);
                        Dispatcher.BeginInvoke(new Action(() => Visibility = Visibility.Visible));
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_VIEW_EXECUTE:
                    if (networkFlag == false)
                    {
                        //原作では成否にかかわらずCMD_SUCCESSだったが、サーバ側の仕様と若干矛盾するので変更した
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_ERR, null, 0);
                        string exeCmd = "";
                        (new CtrlCmdReader(new MemoryStream(cmdData, false))).Read(ref exeCmd);
                        if (exeCmd.Length > 0 && exeCmd[0] == '"')
                        {
                            //形式は("FileName")か("FileName" Arguments..)のどちらか。ほかは拒否してよい
                            int i = exeCmd.IndexOf('"', 1);
                            if (i >= 2 && (exeCmd.Length == i + 1 || exeCmd[i + 1] == ' '))
                            {
                                var startInfo = new ProcessStartInfo(exeCmd.Substring(1, i - 1));
                                startInfo.UseShellExecute = true;
                                if (exeCmd.Length > i + 2)
                                {
                                    startInfo.Arguments = exeCmd.Substring(i + 2);
                                }
                                if (startInfo.FileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (execBat == 0)
                                    {
                                        startInfo.WindowStyle = ProcessWindowStyle.Minimized;
                                    }
                                    else if (execBat == 1)
                                    {
                                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    }
                                }
                                //FileNameは実行ファイルか.batのフルパス。チェックはしない(安全性云々はここで考えることではない)
                                try
                                {
                                    //ShellExecute相当なので.batなどもそのまま与える
                                    using (var process = Process.Start(startInfo))
                                    {
                                        if (process != null)
                                        {
                                            try
                                            {
                                                //"EpgTimer Service"のサービスセキュリティ識別子(Service-specific SID)に対するアクセス許可を追加する
                                                var trustee = new System.Security.Principal.NTAccount("NT Service\\EpgTimer Service");
                                                var trusteeSid = trustee.Translate(typeof(System.Security.Principal.SecurityIdentifier));
                                                var sec = new KernelObjectSecurity(process.Handle);
                                                //SYNCHRONIZE | PROCESS_TERMINATE | PROCESS_SET_INFORMATION
                                                sec.AddAccessRule(new KernelObjectAccessRule(trusteeSid, 0x100000 | 0x01 | 0x0200,
                                                                                             System.Security.AccessControl.AccessControlType.Allow));
                                                sec.Persist(process.Handle);
                                            }
                                            catch { }
                                            var w = new CtrlCmdWriter(new MemoryStream());
                                            w.Write(process.Id);
                                            w.Stream.Close();
                                            res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, w.Stream.ToArray(), 0);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_QUERY_SUSPEND:
                    if (networkFlag == false)
                    {
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, null, 0);

                        ushort param = 0;
                        (new CtrlCmdReader(new MemoryStream(cmdData, false))).Read(ref param);

                        Dispatcher.BeginInvoke(new Action(() => ShowSleepDialog(param)));
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_QUERY_REBOOT:
                    if (networkFlag == false)
                    {
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, null, 0);

                        ushort param = 0;
                        (new CtrlCmdReader(new MemoryStream(cmdData, false))).Read(ref param);

                        byte reboot = (byte)((param & 0xFF00) >> 8);
                        byte suspendMode = (byte)(param & 0x00FF);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            SuspendCheckWindow dlg = new SuspendCheckWindow();
                            dlg.SetMode(reboot, suspendMode);
                            if (dlg.ShowDialog() == true)
                            {
                                CommonManager.CreateSrvCtrl().SendReboot();
                            }
                        }));
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_SRV_STATUS_NOTIFY2:
                    {
                        NotifySrvInfo status = new NotifySrvInfo();
                        var r = new CtrlCmdReader(new MemoryStream(cmdData, false));
                        ushort version = 0;
                        r.Read(ref version);
                        r.Version = version;
                        r.Read(ref status);
                        //通知の巡回カウンタをItem3で返す
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, null, status.param3);
                        Dispatcher.BeginInvoke(new Action(() => NotifyStatus(status)));
                    }
                    break;
            }
            return res;
        }

        private void ShowSleepDialog(ushort param)
        {
            if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoUsePC", 0, SettingPath.TimerSrvIniPath) == 1)
            {
                int ngUsePCTime = IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoUsePCTime", 3, SettingPath.TimerSrvIniPath);

                if (ngUsePCTime == 0 || CommonUtil.GetIdleTimeSec() < ngUsePCTime * 60)
                {
                    return;
                }
            }

            byte suspendMode = (byte)(param & 0x00FF);

            {
                SuspendCheckWindow dlg = new SuspendCheckWindow();
                dlg.SetMode(0, suspendMode);
                if (dlg.ShowDialog() == true)
                {
                    CommonManager.CreateSrvCtrl().SendSuspend(param);
                }
            }
        }

        void NotifyStatus(NotifySrvInfo status)
        {
            Trace.WriteLine((UpdateNotifyItem)status.notifyID);

            switch ((UpdateNotifyItem)status.notifyID)
            {
                case UpdateNotifyItem.EpgData:
                    {
                        CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.EpgData);
                        if (Settings.Instance.NgAutoEpgLoadNW == false)
                        {
                            //EPGデータを遅延更新しない
                            CommonManager.Instance.DB.ReloadEpgData();
                        }
                        epgView.UpdateEpgData();
                        //録画予定ファイル名が変化しているかもしれない
                        CommonManager.Instance.DB.ReloadReserveRecFileNameList();
                        GC.Collect();
                    }
                    break;
                case UpdateNotifyItem.ReserveInfo:
                    {
                        CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.ReserveInfo);
                        //予約情報は常に遅延更新しない
                        CommonManager.Instance.DB.ReloadReserveInfo();
                    }
                    break;
                case UpdateNotifyItem.RecInfo:
                    {
                        CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.RecInfo);
                        recInfoView.UpdateInfo();
                    }
                    break;
                case UpdateNotifyItem.AutoAddEpgInfo:
                    {
                        CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.AutoAddEpgInfo);
                        autoAddView.UpdateAutoAddInfo();
                    }
                    break;
                case UpdateNotifyItem.AutoAddManualInfo:
                    {
                        CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.AutoAddManualInfo);
                        autoAddView.UpdateAutoAddInfo();
                    }
                    break;
                case UpdateNotifyItem.SrvStatus:
                    {
                        if (status.param1 == 1)
                        {
                            taskTray.IconUri = new Uri("pack://application:,,,/Resources/TaskIconRed.ico");
                        }
                        else if (status.param1 == 2)
                        {
                            taskTray.IconUri = new Uri("pack://application:,,,/Resources/TaskIconGreen.ico");
                        }
                        else
                        {
                            taskTray.IconUri = new Uri("pack://application:,,,/Resources/TaskIconBlue.ico");
                        }
                    }
                    break;
                case UpdateNotifyItem.PreRecStart:
                case UpdateNotifyItem.RecStart:
                case UpdateNotifyItem.RecEnd:
                case UpdateNotifyItem.RecTuijyu:
                case UpdateNotifyItem.ChgTuijyu:
                case UpdateNotifyItem.PreEpgCapStart:
                case UpdateNotifyItem.EpgCapStart:
                case UpdateNotifyItem.EpgCapEnd:
                    if (Settings.Instance.NoBallonTips == false)
                    {
                        UpdateNotifyItem notifyID = (UpdateNotifyItem)status.notifyID;
                        string title = notifyID == UpdateNotifyItem.PreRecStart ? "予約録画開始準備" :
                                       notifyID == UpdateNotifyItem.RecStart ? "録画開始" :
                                       notifyID == UpdateNotifyItem.RecEnd ? "録画終了" :
                                       notifyID == UpdateNotifyItem.RecTuijyu ? "追従発生" :
                                       notifyID == UpdateNotifyItem.ChgTuijyu ? "番組変更" : "EPG取得";
                        string tips = notifyID == UpdateNotifyItem.EpgCapStart ? "開始" :
                                      notifyID == UpdateNotifyItem.EpgCapEnd ? "終了" : status.param4;
                        taskTray.ShowBalloonTip(title, tips, 10 * 1000, Settings.Instance.BalloonTipRealtime);
                    }
                    CommonManager.Instance.NotifyLogList.Add(status);
                    break;
                default:
                    break;
            }

            ReserveData item = CommonManager.Instance.DB.GetNextReserve();
            if (item != null)
            {
                taskTray.Text = "次の予約：" + item.StationName + item.StartTime.ToString(" M\\/d(ddd) H\\:mm-") +
                                item.StartTime.AddSeconds(item.DurationSecond).ToString("H\\:mm ") + item.Title;
            }
            else
            {
                taskTray.Text = "次の予約なし";
            }
        }

        public void SearchJumpTargetProgram(object target)
        {
            grid_main.Effect = new System.Windows.Media.Effects.BlurEffect();
            //効果がかかるまで遅延
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                tabItem_epg.IsSelected = true;
                epgView.SearchJumpTargetProgram(target);
                //ジャンプが済むまで遅延
                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    grid_main.Effect = null;
                }));
            }));
        }

        public SearchWindow CreateSearchWindow()
        {
            SearchWindow search = null;
            foreach (Window w in OwnedWindows)
            {
                //表示状態をなるべく引き継ぐ。幅と高さは内部で引き継がれる
                if (w is SearchWindow)
                {
                    int index = ((SearchWindow)w).tabControl.SelectedIndex;
                    double left = w.Left;
                    double top = w.Top;
                    w.Close();
                    if (search == null)
                    {
                        search = new SearchWindow();
                        search.WindowStartupLocation = WindowStartupLocation.Manual;
                        search.tabControl.SelectedIndex = index;
                        search.Left = left;
                        search.Top = top;
                    }
                }
            }
            search = search ?? new SearchWindow();
            search.Owner = this;
            //所有する他のウィンドウをアクティブにできないときオーナーが最背面に移動することがあるため
            search.Closed += (sender, e) =>
            {
                if (OwnedWindows.Cast<Window>().All(a => a.IsActive == false))
                {
                    Activate();
                }
            };
            return search;
        }

        public void SwapOwnedReserveWindow(Window re)
        {
            foreach (Window w in OwnedWindows)
            {
                //同系統ウィンドウの表示状態をなるべく引き継ぐ
                if (w is AddReserveEpgWindow || w is ChgReserveWindow)
                {
                    if (re is AddReserveEpgWindow || re is ChgReserveWindow)
                    {
                        re.WindowStartupLocation = WindowStartupLocation.Manual;
                        int openMode = w is AddReserveEpgWindow ? ((AddReserveEpgWindow)w).GetOpenMode() : ((ChgReserveWindow)w).GetOpenMode();
                        if (re is AddReserveEpgWindow)
                        {
                            ((AddReserveEpgWindow)re).SetOpenMode(openMode);
                        }
                        else
                        {
                            ((ChgReserveWindow)re).SetOpenMode(openMode);
                        }
                        re.Left = w.Left;
                        re.Top = w.Top;
                        if ((w is AddReserveEpgWindow) == (re is AddReserveEpgWindow))
                        {
                            re.Width = w.Width;
                            re.Height = w.Height;
                        }
                    }
                    w.Close();
                }
                else if (w is RecInfoDescWindow)
                {
                    if (re is RecInfoDescWindow)
                    {
                        re.WindowStartupLocation = WindowStartupLocation.Manual;
                        ((RecInfoDescWindow)re).tabControl.SelectedIndex = ((RecInfoDescWindow)w).tabControl.SelectedIndex;
                        re.Left = w.Left;
                        re.Top = w.Top;
                        re.Width = w.Width;
                        re.Height = w.Height;
                    }
                    w.Close();
                }
                else if (w is AddManualAutoAddWindow)
                {
                    if (re is AddManualAutoAddWindow)
                    {
                        re.WindowStartupLocation = WindowStartupLocation.Manual;
                        re.Left = w.Left;
                        re.Top = w.Top;
                        re.Width = w.Width;
                        re.Height = w.Height;
                    }
                    w.Close();
                }
            }
            if (re != null)
            {
                re.Owner = this;
                //所有する他のウィンドウをアクティブにできないときオーナーが最背面に移動することがあるため
                re.Closed += (sender, e) =>
                {
                    if (OwnedWindows.Cast<Window>().All(a => a.IsActive == false))
                    {
                        Activate();
                    }
                };
            }
        }
    }
}
