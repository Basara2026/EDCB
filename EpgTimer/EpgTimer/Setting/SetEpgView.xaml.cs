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
using System.Windows.Markup;

namespace EpgTimer.Setting
{
    /// <summary>
    /// SetEpgView.xaml の相互作用ロジック
    /// </summary>
    public partial class SetEpgView : UserControl
    {
        private bool initializingColor;
        private bool applyingServiceAliasColumns;

        public SetEpgView()
        {
            InitializeComponent();

            //サービス名の一覧の各項目の枠の幅を変えたら記憶する
            var widthDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));
            foreach (GridViewColumn column in gridView_serviceAlias.Columns)
            {
                widthDescriptor.AddValueChanged(column, (sender, e) => SaveServiceAliasColumnWidth());
            }

            foreach (string s in CommonManager.GetSortedFontNames())
            {
                comboBox_font.Items.Add(s);
                comboBox_fontTitle.Items.Add(s);
                comboBox_fontHeader.Items.Add(s);
                comboBox_fontHeaderWeek.Items.Add(s);
                comboBox_fontTimeHeader.Items.Add(s);
            }
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var settings = (Settings)DataContext;
            if (settings == null)
            {
                grid_basic.DataContext = null;
                grid_color.DataContext = null;
                listView_serviceAlias.ItemsSource = null;
            }
            else
            {
                //番組表の表示上のサービス名の変更(編集)用の一覧。入力は設定のコピーに直接反映する
                var aliasItems = new List<ServiceAliasItem>();
                foreach (ChSet5Item ch in ChSet5.Instance.ChListSelected)
                {
                    aliasItems.Add(new ServiceAliasItem(ch.Key, ch.ServiceName, CommonManager.ConvertNetworkNameText(ch.ONID),
                        settings.GetServiceDisplayName(ch.ONID, ch.TSID, ch.SID, ""), settings.SetServiceDisplayName));
                }
                listView_serviceAlias.ItemsSource = aliasItems;
                //記憶している枠の幅があれば反映する
                List<double> widths = Settings.Instance.ServiceAliasColumnWidth;
                if (widths != null && widths.Count == gridView_serviceAlias.Columns.Count && widths.All(w => w >= 20 && w <= 2000))
                {
                    applyingServiceAliasColumns = true;
                    try
                    {
                        for (int i = 0; i < widths.Count; i++)
                        {
                            gridView_serviceAlias.Columns[i].Width = widths[i];
                        }
                    }
                    finally
                    {
                        applyingServiceAliasColumns = false;
                    }
                }
                grid_basic.DataContext = settings.EpgSettingList[radioButton_basicDesignSub1.IsChecked == true ? 1 :
                                                                 radioButton_basicDesignSub2.IsChecked == true ? 2 : 0];
                grid_color.DataContext = settings.EpgSettingList[radioButton_colorDesignSub1.IsChecked == true ? 1 :
                                                                 radioButton_colorDesignSub2.IsChecked == true ? 2 : 0];
                listBox_tab.Items.Clear();
                foreach (CustomEpgTabInfo info in settings.CustomEpgTabList)
                {
                    listBox_tab.Items.Add(info);
                }
                if (listBox_tab.Items.Count > 0)
                {
                    listBox_tab.SelectedIndex = 0;
                }
            }
        }

        private void SaveServiceAliasColumnWidth()
        {
            if (applyingServiceAliasColumns)
            {
                return;
            }
            //設定画面を閉じたあとも残るよう、設定のコピーではなく現在の設定に直接書き込む(新しいリストを作って置き換える)
            Settings.Instance.ServiceAliasColumnWidth = gridView_serviceAlias.Columns
                .Select(c => double.IsNaN(c.Width) ? c.ActualWidth : c.Width).ToList();
        }

        //「表示位置」のプルダウンを右クリックしたときに出る、グレーアウトした「切り取り/コピー/貼り付け」を出さない
        private void comboBox_NoContextMenu_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        //キーボード(アプリケーションキー、Shift+F10)からのメニュー表示も同様
        private void comboBox_NoContextMenu_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Apps || (e.Key == Key.F10 && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None))
            {
                e.Handled = true;
            }
        }

        public class ServiceAliasItem
        {
            private readonly Action<ulong, string> changed;
            private string displayName;

            public ServiceAliasItem(ulong key, string originalName, string networkName, string displayName, Action<ulong, string> changed)
            {
                Key = key;
                OriginalName = originalName;
                NetworkName = networkName;
                this.displayName = displayName;
                this.changed = changed;
            }
            public ulong Key { get; private set; }
            public string OriginalName { get; private set; }
            public string NetworkName { get; private set; }
            public string DisplayName
            {
                get { return displayName; }
                set
                {
                    displayName = value;
                    changed(Key, value);
                }
            }
        }

        private void grid_color_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var epgSetting = (EpgSetting)grid_color.DataContext;
            if (epgSetting != null)
            {
                checkBox_colorMode.IsChecked = !epgSetting.EpgDarkMode;
                checkBox_darkMode.IsChecked = epgSetting.EpgDarkMode;
                List<string> ccList = epgSetting.ContentColorList;
                List<uint> cuList = epgSetting.ContentCustColorList;
                initializingColor = true;
                comboBox0.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x00]));
                comboBox1.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x01]));
                comboBox2.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x02]));
                comboBox3.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x03]));
                comboBox4.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x04]));
                comboBox5.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x05]));
                comboBox6.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x06]));
                comboBox7.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x07]));
                comboBox8.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x08]));
                comboBox9.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x09]));
                comboBox10.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x0A]));
                comboBox11.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x0B]));
                comboBox12.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x0F]));
                comboBox13.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(ccList[0x10]));
                comboBox_service.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.ServiceColor));
                comboBox_reserveNormal.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.ReserveRectColorNormal));
                comboBox_reserveNo.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.ReserveRectColorNo));
                comboBox_reserveNoTuner.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.ReserveRectColorNoTuner));
                comboBox_reserveWarning.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.ReserveRectColorWarning));
                comboBox_colorTitle1.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.TitleColor1));
                comboBox_colorTitle2.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.TitleColor2));
                comboBox_serviceText.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.ServiceTextColor));
                comboBox_weekDayText.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.WeekDayTextColor));
                comboBox_timeText.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.TimeTextColor));
                comboBox_blurTitle.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.TitleBlurColor));
                comboBox_blurBody.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.BodyBlurColor));
                comboBox_blurService.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.ServiceBlurColor));
                comboBox_blurWeekDay.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.WeekDayBlurColor));
                comboBox_blurTime.SelectedIndex = Math.Max(0, ColorDef.BrushNames.IndexOfKey(epgSetting.TimeBlurColor));
                initializingColor = false;
                button_colorTitle1.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.TitleCustColor1));
                button_colorTitle2.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.TitleCustColor2));
                button_serviceText.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.ServiceTextCustColor));
                button_weekDayText.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.WeekDayTextCustColor));
                button_timeText.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.TimeTextCustColor));
                button_blurTitle.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.TitleBlurCustColor));
                button_blurBody.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.BodyBlurCustColor));
                button_blurService.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.ServiceBlurCustColor));
                button_blurWeekDay.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.WeekDayBlurCustColor));
                button_blurTime.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.TimeBlurCustColor));
                button0.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x00]));
                button1.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x01]));
                button2.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x02]));
                button3.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x03]));
                button4.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x04]));
                button5.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x05]));
                button6.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x06]));
                button7.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x07]));
                button8.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x08]));
                button9.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x09]));
                button10.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x0A]));
                button11.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x0B]));
                button12.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x0F]));
                button13.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x10]));
                button_service.Background = new SolidColorBrush(ColorDef.FromUInt(epgSetting.ServiceCustColor));
                button_epgBack.Background = new SolidColorBrush(Color.FromArgb(epgSetting.EpgBackColorA, epgSetting.EpgBackColorR,
                                                                               epgSetting.EpgBackColorG, epgSetting.EpgBackColorB));
                button14.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x11]));
                button15.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x12]));
                button16.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x13]));
                button17.Background = new SolidColorBrush(ColorDef.FromUInt(cuList[0x14]));
            }
        }

        private void OnUpdateColor()
        {
            var epgSetting = (EpgSetting)grid_color.DataContext;
            if (epgSetting != null && initializingColor == false)
            {
                List<string> ccList = epgSetting.ContentColorList;
                List<uint> cuList = epgSetting.ContentCustColorList;
                ccList[0x00] = ((KeyValuePair<string, SolidColorBrush>)comboBox0.SelectedItem).Key;
                ccList[0x01] = ((KeyValuePair<string, SolidColorBrush>)comboBox1.SelectedItem).Key;
                ccList[0x02] = ((KeyValuePair<string, SolidColorBrush>)comboBox2.SelectedItem).Key;
                ccList[0x03] = ((KeyValuePair<string, SolidColorBrush>)comboBox3.SelectedItem).Key;
                ccList[0x04] = ((KeyValuePair<string, SolidColorBrush>)comboBox4.SelectedItem).Key;
                ccList[0x05] = ((KeyValuePair<string, SolidColorBrush>)comboBox5.SelectedItem).Key;
                ccList[0x06] = ((KeyValuePair<string, SolidColorBrush>)comboBox6.SelectedItem).Key;
                ccList[0x07] = ((KeyValuePair<string, SolidColorBrush>)comboBox7.SelectedItem).Key;
                ccList[0x08] = ((KeyValuePair<string, SolidColorBrush>)comboBox8.SelectedItem).Key;
                ccList[0x09] = ((KeyValuePair<string, SolidColorBrush>)comboBox9.SelectedItem).Key;
                ccList[0x0A] = ((KeyValuePair<string, SolidColorBrush>)comboBox10.SelectedItem).Key;
                ccList[0x0B] = ((KeyValuePair<string, SolidColorBrush>)comboBox11.SelectedItem).Key;
                ccList[0x0F] = ((KeyValuePair<string, SolidColorBrush>)comboBox12.SelectedItem).Key;
                ccList[0x10] = ((KeyValuePair<string, SolidColorBrush>)comboBox13.SelectedItem).Key;
                epgSetting.TitleColor1 = ((KeyValuePair<string, SolidColorBrush>)comboBox_colorTitle1.SelectedItem).Key;
                epgSetting.TitleColor2 = ((KeyValuePair<string, SolidColorBrush>)comboBox_colorTitle2.SelectedItem).Key;
                epgSetting.TitleCustColor1 = ColorDef.ToUInt(((SolidColorBrush)button_colorTitle1.Background).Color);
                epgSetting.TitleCustColor2 = ColorDef.ToUInt(((SolidColorBrush)button_colorTitle2.Background).Color);
                epgSetting.ServiceTextColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_serviceText.SelectedItem).Key;
                epgSetting.ServiceTextCustColor = ColorDef.ToUInt(((SolidColorBrush)button_serviceText.Background).Color);
                epgSetting.WeekDayTextColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_weekDayText.SelectedItem).Key;
                epgSetting.WeekDayTextCustColor = ColorDef.ToUInt(((SolidColorBrush)button_weekDayText.Background).Color);
                epgSetting.TimeTextColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_timeText.SelectedItem).Key;
                epgSetting.TimeTextCustColor = ColorDef.ToUInt(((SolidColorBrush)button_timeText.Background).Color);
                epgSetting.TitleBlurColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_blurTitle.SelectedItem).Key;
                epgSetting.TitleBlurCustColor = ColorDef.ToUInt(((SolidColorBrush)button_blurTitle.Background).Color);
                epgSetting.BodyBlurColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_blurBody.SelectedItem).Key;
                epgSetting.BodyBlurCustColor = ColorDef.ToUInt(((SolidColorBrush)button_blurBody.Background).Color);
                epgSetting.ServiceBlurColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_blurService.SelectedItem).Key;
                epgSetting.ServiceBlurCustColor = ColorDef.ToUInt(((SolidColorBrush)button_blurService.Background).Color);
                epgSetting.WeekDayBlurColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_blurWeekDay.SelectedItem).Key;
                epgSetting.WeekDayBlurCustColor = ColorDef.ToUInt(((SolidColorBrush)button_blurWeekDay.Background).Color);
                epgSetting.TimeBlurColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_blurTime.SelectedItem).Key;
                epgSetting.TimeBlurCustColor = ColorDef.ToUInt(((SolidColorBrush)button_blurTime.Background).Color);
                epgSetting.ServiceColor = ((KeyValuePair<string, SolidColorBrush>)comboBox_service.SelectedItem).Key;
                epgSetting.ReserveRectColorNormal = ((KeyValuePair<string, SolidColorBrush>)comboBox_reserveNormal.SelectedItem).Key;
                epgSetting.ReserveRectColorNo = ((KeyValuePair<string, SolidColorBrush>)comboBox_reserveNo.SelectedItem).Key;
                epgSetting.ReserveRectColorNoTuner = ((KeyValuePair<string, SolidColorBrush>)comboBox_reserveNoTuner.SelectedItem).Key;
                epgSetting.ReserveRectColorWarning = ((KeyValuePair<string, SolidColorBrush>)comboBox_reserveWarning.SelectedItem).Key;
                cuList[0x00] = ColorDef.ToUInt(((SolidColorBrush)button0.Background).Color);
                cuList[0x01] = ColorDef.ToUInt(((SolidColorBrush)button1.Background).Color);
                cuList[0x02] = ColorDef.ToUInt(((SolidColorBrush)button2.Background).Color);
                cuList[0x03] = ColorDef.ToUInt(((SolidColorBrush)button3.Background).Color);
                cuList[0x04] = ColorDef.ToUInt(((SolidColorBrush)button4.Background).Color);
                cuList[0x05] = ColorDef.ToUInt(((SolidColorBrush)button5.Background).Color);
                cuList[0x06] = ColorDef.ToUInt(((SolidColorBrush)button6.Background).Color);
                cuList[0x07] = ColorDef.ToUInt(((SolidColorBrush)button7.Background).Color);
                cuList[0x08] = ColorDef.ToUInt(((SolidColorBrush)button8.Background).Color);
                cuList[0x09] = ColorDef.ToUInt(((SolidColorBrush)button9.Background).Color);
                cuList[0x0A] = ColorDef.ToUInt(((SolidColorBrush)button10.Background).Color);
                cuList[0x0B] = ColorDef.ToUInt(((SolidColorBrush)button11.Background).Color);
                cuList[0x0F] = ColorDef.ToUInt(((SolidColorBrush)button12.Background).Color);
                cuList[0x10] = ColorDef.ToUInt(((SolidColorBrush)button13.Background).Color);
                epgSetting.ServiceCustColor = ColorDef.ToUInt(((SolidColorBrush)button_service.Background).Color);
                epgSetting.EpgBackColorA = ((SolidColorBrush)button_epgBack.Background).Color.A;
                epgSetting.EpgBackColorR = ((SolidColorBrush)button_epgBack.Background).Color.R;
                epgSetting.EpgBackColorG = ((SolidColorBrush)button_epgBack.Background).Color.G;
                epgSetting.EpgBackColorB = ((SolidColorBrush)button_epgBack.Background).Color.B;
                cuList[0x11] = ColorDef.ToUInt(((SolidColorBrush)button14.Background).Color);
                cuList[0x12] = ColorDef.ToUInt(((SolidColorBrush)button15.Background).Color);
                cuList[0x13] = ColorDef.ToUInt(((SolidColorBrush)button16.Background).Color);
                cuList[0x14] = ColorDef.ToUInt(((SolidColorBrush)button17.Background).Color);
            }
        }

        private void OnUpdateTabListBox()
        {
            ((Settings)DataContext).CustomEpgTabList.Clear();
            ((Settings)DataContext).CustomEpgTabList.AddRange(listBox_tab.Items.OfType<CustomEpgTabInfo>());
        }

        private void button_tab_add_Click(object sender, RoutedEventArgs e)
        {
            EpgDataViewSettingWindow dlg = new EpgDataViewSettingWindow();
            PresentationSource topWindow = PresentationSource.FromVisual(this);
            if (topWindow != null)
            {
                dlg.Owner = (Window)topWindow.RootVisual;
            }
            if (dlg.ShowDialog() == true)
            {
                listBox_tab.Items.Add(dlg.GetSetting());
                OnUpdateTabListBox();
            }
        }

        private void button_tab_chg_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_tab.SelectedItem != null)
            {
                EpgDataViewSettingWindow dlg = new EpgDataViewSettingWindow();
                PresentationSource topWindow = PresentationSource.FromVisual(this);
                if (topWindow != null)
                {
                    dlg.Owner = (Window)topWindow.RootVisual;
                }
                dlg.SetDefSetting((CustomEpgTabInfo)listBox_tab.SelectedItem);
                if (dlg.ShowDialog() == true)
                {
                    listBox_tab.SelectedItem = listBox_tab.Items[listBox_tab.SelectedIndex] = dlg.GetSetting();
                    OnUpdateTabListBox();
                }
            }
        }

        private void button_tab_del_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_tab.SelectedItem != null)
            {
                listBox_tab.Items.RemoveAt(listBox_tab.SelectedIndex);
                if (listBox_tab.Items.Count > 0)
                {
                    listBox_tab.SelectedIndex = 0;
                }
                OnUpdateTabListBox();
            }
        }

        private void button_tab_up_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_tab.SelectedItem != null)
            {
                if (listBox_tab.SelectedIndex >= 1)
                {
                    object temp = listBox_tab.SelectedItem;
                    int index = listBox_tab.SelectedIndex;
                    listBox_tab.Items.RemoveAt(listBox_tab.SelectedIndex);
                    listBox_tab.Items.Insert(index - 1, temp);
                    listBox_tab.SelectedIndex = index - 1;
                    OnUpdateTabListBox();
                }
            }
        }

        private void buttontab_down_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_tab.SelectedItem != null)
            {
                if (listBox_tab.SelectedIndex < listBox_tab.Items.Count - 1)
                {
                    object temp = listBox_tab.SelectedItem;
                    int index = listBox_tab.SelectedIndex;
                    listBox_tab.Items.RemoveAt(listBox_tab.SelectedIndex);
                    listBox_tab.Items.Insert(index + 1, temp);
                    listBox_tab.SelectedIndex = index + 1;
                    OnUpdateTabListBox();
                }
            }
        }

        /// <summary>
        /// 「カラーモード」「ダークモード」のどちらか一方にチェックが入るようにする。
        /// モードを切り替えたときは、文字色とぼかしをそのモードに合う値に変更する(変更後の値は、通常の設定として後から自由に変更できる)
        /// </summary>
        private void checkBox_displayMode_Click(object sender, RoutedEventArgs e)
        {
            var epgSetting = (EpgSetting)grid_color.DataContext;
            if (epgSetting == null)
            {
                return;
            }
            bool dark = sender == checkBox_darkMode;
            if (dark != epgSetting.EpgDarkMode)
            {
                epgSetting.EpgDarkMode = dark;
                if (dark)
                {
                    //背景が黒なので、明るいグレーの文字にする
                    epgSetting.TitleColor1 = "LightGray";
                    epgSetting.TitleBlurRadius = 0;
                    epgSetting.TitleColor2 = "LightGray";
                    epgSetting.BodyBlurRadius = 0;
                }
                else
                {
                    epgSetting.TitleColor1 = "Black";
                    epgSetting.TitleBlurRadius = 5;
                    epgSetting.TitleColor2 = "Black";
                    epgSetting.BodyBlurRadius = 5;
                }
            }
            //画面の表示を更新する(チェックの状態も、設定に合わせて直る)
            grid_color.DataContext = null;
            grid_color.DataContext = epgSetting;
        }

        private void button_Color_Click(object sender, RoutedEventArgs e)
        {
            //色の設定ダイアログ(透明度も選べる)
            Color item = ((SolidColorBrush)((Button)sender).Background).Color;
            var dlg = new ColorPickerWindow(item, (Window)PresentationSource.FromVisual(this).RootVisual);
            if (dlg.ShowDialog() == true)
            {
                ((SolidColorBrush)((Button)sender).Background).Color = dlg.SelectedColor;
                OnUpdateColor();
            }
        }

        /// <summary>
        /// プリセット(ジャンル別背景色、サービス背景、番組表背景、予約枠)を設定する。文字色とぼかしは変更しない。
        /// 色は、読みやすさを確認した設定画面の画像に合わせている
        /// </summary>
        private void button_setPreset_Click(object sender, RoutedEventArgs e)
        {
            var epgSetting = (EpgSetting)grid_color.DataContext;
            if (epgSetting != null)
            {
                List<string> ccList = epgSetting.ContentColorList;
                List<uint> cuList = epgSetting.ContentCustColorList;
                //ジャンル別背景色
                ccList[0x00] = "RoyalBlue";        // ニュース／報道
                ccList[0x01] = "DarkGreen";         // スポーツ
                ccList[0x02] = "CornflowerBlue";   // 情報／ワイドショー
                ccList[0x03] = "Chocolate";        // ドラマ
                ccList[0x04] = "MediumPurple";     // 音楽
                ccList[0x05] = "SteelBlue";        // バラエティ
                ccList[0x06] = "Teal";             // 映画
                ccList[0x07] = "Salmon";           // アニメ／特撮
                ccList[0x08] = "SeaGreen";         // ドキュメンタリー／教養
                ccList[0x09] = "ForestGreen";      // 劇場／公演
                ccList[0x0A] = "Coral";            // 趣味／教育
                ccList[0x0B] = "DarkCyan";         // 福祉
                ccList[0x0F] = "PaleVioletRed";    // その他
                ccList[0x10] = "Sienna";           // なし
                for (int i = 0x00; i <= 0x0B; i++)
                {
                    cuList[i] = 0xFFFFFFFF;
                }
                cuList[0x0F] = 0xFFFFFFFF;
                cuList[0x10] = 0xFFFFFFFF;
                //サービス背景、番組表背景
                epgSetting.ServiceColor = "Black";
                epgSetting.ServiceCustColor = 0xFFFFFFFF;
                epgSetting.EpgBackColorA = 128;
                epgSetting.EpgBackColorR = 50;
                epgSetting.EpgBackColorG = 50;
                epgSetting.EpgBackColorB = 50;
                //予約枠
                epgSetting.ReserveRectColorNormal = "Lime";
                epgSetting.ReserveRectColorNo = "Black";
                epgSetting.ReserveRectColorNoTuner = "Red";
                epgSetting.ReserveRectColorWarning = "Yellow";
                grid_color.DataContext = null;
                grid_color.DataContext = epgSetting;
            }
        }

        private void comboBox_color_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OnUpdateColor();
        }

        private void radioButton_basicDesign_Checked(object sender, RoutedEventArgs e)
        {
            Settings settings = (Settings)DataContext;
            if (settings != null)
            {
                grid_basic.DataContext = settings.EpgSettingList[radioButton_basicDesignSub1.IsChecked == true ? 1 :
                                                                 radioButton_basicDesignSub2.IsChecked == true ? 2 : 0];
                if (radioButton_colorDesignDefault.IsChecked != radioButton_basicDesignDefault.IsChecked ||
                    radioButton_colorDesignSub1.IsChecked != radioButton_basicDesignSub1.IsChecked ||
                    radioButton_colorDesignSub2.IsChecked != radioButton_basicDesignSub2.IsChecked)
                {
                    radioButton_colorDesignDefault.IsChecked = radioButton_basicDesignDefault.IsChecked;
                    radioButton_colorDesignSub1.IsChecked = radioButton_basicDesignSub1.IsChecked;
                    radioButton_colorDesignSub2.IsChecked = radioButton_basicDesignSub2.IsChecked;
                }
            }
        }

        private void radioButton_colorDesign_Checked(object sender, RoutedEventArgs e)
        {
            Settings settings = (Settings)DataContext;
            if (settings != null)
            {
                grid_color.DataContext = settings.EpgSettingList[radioButton_colorDesignSub1.IsChecked == true ? 1 :
                                                                 radioButton_colorDesignSub2.IsChecked == true ? 2 : 0];
                if (radioButton_basicDesignDefault.IsChecked != radioButton_colorDesignDefault.IsChecked ||
                    radioButton_basicDesignSub1.IsChecked != radioButton_colorDesignSub1.IsChecked ||
                    radioButton_basicDesignSub2.IsChecked != radioButton_colorDesignSub2.IsChecked)
                {
                    radioButton_basicDesignDefault.IsChecked = radioButton_colorDesignDefault.IsChecked;
                    radioButton_basicDesignSub1.IsChecked = radioButton_colorDesignSub1.IsChecked;
                    radioButton_basicDesignSub2.IsChecked = radioButton_colorDesignSub2.IsChecked;
                }
            }
        }
    }
}
