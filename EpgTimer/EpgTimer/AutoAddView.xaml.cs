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

namespace EpgTimer
{
    /// <summary>
    /// AutoAddView.xaml の相互作用ロジック
    /// </summary>
    public partial class AutoAddView : UserControl
    {
        public AutoAddView()
        {
            InitializeComponent();

            //スクロールバーの操作性のため
            tabControl.BorderThickness = new Thickness(0, tabControl.BorderThickness.Top, 0, 0);
            tabControl.Padding = new Thickness(0, tabControl.Padding.Top, 0, 0);

            //「ボタンレイアウト変更」のプルダウンメニューの行を、タブの切り替えに合わせて更新できるようにする
            tabControl.SelectionChanged += (sender, e) =>
            {
                //中のリストなどの選択変更は対象外にする
                if (ReferenceEquals(e.OriginalSource, tabControl))
                {
                    EventHandler handler = NavigationChanged;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }
                }
            };
        }

        /// <summary>
        /// 選択しているタブ(EPG予約/プログラム予約)が変わったときに発生する
        /// </summary>
        public event EventHandler NavigationChanged;

        /// <summary>
        /// タブの見出しの行を表示するかどうか。「ボタンレイアウト変更」のときは、ウィンドウ上部のプルダウンメニューで切り替えるので隠す
        /// </summary>
        public void SetHeaderVisible(bool visible)
        {
            TabHeaderLayout.SetHeaderVisible(tabControl, visible);
        }

        /// <summary>
        /// タブの名前(EPG予約、プログラム予約)の一覧
        /// </summary>
        public List<string> GetTabNames()
        {
            return tabControl.Items.Cast<TabItem>().Select(tab => tab.Header as string ?? string.Empty).ToList();
        }

        /// <summary>
        /// 選択しているタブの番号
        /// </summary>
        public int SelectedTabIndex
        {
            get { return tabControl.SelectedIndex; }
            set
            {
                if (value >= 0 && value < tabControl.Items.Count)
                {
                    tabControl.SelectedIndex = value;
                }
            }
        }

        public void SaveSize()
        {
            epgAutoAddView.SaveSize();
            manualAutoAddView.SaveSize();
        }

        public void UpdateAutoAddInfo()
        {
            epgAutoAddView.UpdateInfo();
            manualAutoAddView.UpdateInfo();
        }

    }
}
