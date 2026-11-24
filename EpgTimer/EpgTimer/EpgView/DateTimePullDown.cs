using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EpgTimer.EpgView
{
    /// <summary>
    /// 番組表の日付と時間帯を切り替えるプルダウンメニュー(日付、時間帯の2つ)
    /// </summary>
    public class DateTimePullDown : StackPanel
    {
        private readonly ComboBox comboDate;
        private readonly ComboBox comboTime;
        private readonly List<DateTime> days = new List<DateTime>();
        private bool updating = false;

        /// <summary>
        /// 利用者が日付または時間帯を選んだとき。選んだ日時(日付+時間帯の開始時刻)を渡す
        /// </summary>
        public event Action<DateTime> TimeSelected;

        public DateTimePullDown()
        {
            Orientation = Orientation.Horizontal;
            var style = (Style)Application.Current.FindResource("AppComboBoxStyle");
            comboDate = new ComboBox() { Style = style };
            comboTime = new ComboBox() { Style = style, Margin = (Thickness)Application.Current.FindResource("AppMarginL") };
            foreach (int hour in new[] { 0, 6, 12, 18 })
            {
                comboTime.Items.Add(hour.ToString() + "時");
            }
            comboDate.SelectionChanged += OnSelectionChanged;
            comboTime.SelectionChanged += OnSelectionChanged;
            Children.Add(comboDate);
            Children.Add(comboTime);
        }

        public void ClearInfo()
        {
            updating = true;
            days.Clear();
            comboDate.Items.Clear();
            comboDate.SelectedIndex = -1;
            comboTime.SelectedIndex = -1;
            updating = false;
        }

        /// <summary>
        /// 選べる日付を設定する(startTimeの日からendTimeまで。ただし今日から7日後までで、8日後以降は表示しない)
        /// </summary>
        public void SetTime(DateTime startTime, DateTime endTime)
        {
            ClearInfo();
            if (startTime != default(DateTime))
            {
                //番組データの終端が8日後の早朝にかかることがあるが、番組表の日付は1週間後(今日+7日)までにする
                DateTime lastDay = DateTime.UtcNow.AddHours(9).Date.AddDays(7);
                for (DateTime itemTime = startTime.Date; itemTime < endTime && itemTime <= lastDay; itemTime = itemTime.AddDays(1))
                {
                    days.Add(itemTime);
                    comboDate.Items.Add(itemTime.ToString("M\\/d(ddd)"));
                }
            }
        }

        /// <summary>
        /// 現在表示している日時の位置を選択状態にする(イベントは発生させない)
        /// </summary>
        public void SetScrollTime(DateTime time)
        {
            int index = days.IndexOf(time.Date);
            int timeIndex = Math.Min(time.Hour / 6, 3);
            if (index < 0 && days.Count > 0 && time.Date > days[days.Count - 1])
            {
                //表示しない日付(8日後以降)にかかっているときは、最後の日付の最後の時間帯を選択状態にする
                index = days.Count - 1;
                timeIndex = 3;
            }
            updating = true;
            comboDate.SelectedIndex = index;
            comboTime.SelectedIndex = index < 0 ? -1 : timeIndex;
            updating = false;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updating || comboDate.SelectedIndex < 0 || comboTime.SelectedIndex < 0 || comboDate.SelectedIndex >= days.Count)
            {
                return;
            }
            if (TimeSelected != null)
            {
                TimeSelected(days[comboDate.SelectedIndex].AddHours(comboTime.SelectedIndex * 6));
            }
        }
    }
}
